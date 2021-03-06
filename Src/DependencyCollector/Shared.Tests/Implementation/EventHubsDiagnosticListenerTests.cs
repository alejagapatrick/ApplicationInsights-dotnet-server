﻿namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
#if !NETCORE
    using Microsoft.ApplicationInsights.Web.TestFramework;
#else
    using Microsoft.ApplicationInsights.Tests;
#endif
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventHubsDiagnosticListenerTests
    {
        private TelemetryConfiguration configuration;
        private List<ITelemetry> sentItems;

        [TestInitialize]
        public void TestInitialize()
        {
            this.configuration = new TelemetryConfiguration();
            this.sentItems = new List<ITelemetry>();
            this.configuration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => this.sentItems.Add(item), EndpointAddress = "https://dc.services.visualstudio.com/v2/track" };
            this.configuration.InstrumentationKey = Guid.NewGuid().ToString();
        }

        [TestCleanup]
        public void CleanUp()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        [TestMethod]
        public void DiagnosticEventWithoutActivityIsIgnored()
        {
            using (var listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            using (var module = new DependencyTrackingTelemetryModule())
            {
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);

                listener.Write(
                    "Microsoft.Azure.EventHubs.Send.Stop", 
                    new
                    {
                        Entity = "ehname",
                        Endpoint = new Uri("sb://eventhubname.servicebus.windows.net/"),
                        PartitionKey = "SomePartitionKeyHere",
                        Status = TaskStatus.RanToCompletion
                    });

                Assert.IsFalse(this.sentItems.Any());
            }
        }

        [TestMethod]
        public void EventHubsSuccessfulSendIsHandled()
        {
            using (var listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            using (var module = new DependencyTrackingTelemetryModule())
            {
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);

                Activity parentActivity = new Activity("parent").AddBaggage("k1", "v1").Start();
                var telemetry = this.TrackOperation<DependencyTelemetry>(listener, "Microsoft.Azure.EventHubs.Send", TaskStatus.RanToCompletion);

                Assert.IsNotNull(telemetry);
                Assert.AreEqual("Send", telemetry.Name);
                Assert.AreEqual(RemoteDependencyConstants.AzureEventHubs, telemetry.Type);
                Assert.AreEqual("sb://eventhubname.servicebus.windows.net/ | ehname", telemetry.Target);
                Assert.IsTrue(telemetry.Success.Value);

                Assert.AreEqual(parentActivity.Id, telemetry.Context.Operation.ParentId);
                Assert.AreEqual(parentActivity.RootId, telemetry.Context.Operation.Id);
                Assert.AreEqual("v1", telemetry.Properties["k1"]);
                Assert.AreEqual("eventhubname.servicebus.windows.net", telemetry.Properties["peer.hostname"]);
                Assert.AreEqual("ehname", telemetry.Properties["eh.event_hub_name"]);
                Assert.AreEqual("SomePartitionKeyHere", telemetry.Properties["eh.partition_key"]);
                Assert.AreEqual("EventHubClient1(ehname)", telemetry.Properties["eh.client_id"]);
            }
        }

        [TestMethod]
        public void EventHubsSuccessfulSendIsHandledWithoutParent()
        {
            using (var listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            using (var module = new DependencyTrackingTelemetryModule())
            {
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);

                var telemetry = this.TrackOperation<DependencyTelemetry>(listener, "Microsoft.Azure.EventHubs.Send", TaskStatus.RanToCompletion);

                Assert.IsNotNull(telemetry);
                Assert.AreEqual("Send", telemetry.Name);
                Assert.AreEqual(RemoteDependencyConstants.AzureEventHubs, telemetry.Type);
                Assert.AreEqual("sb://eventhubname.servicebus.windows.net/ | ehname", telemetry.Target);
                Assert.IsTrue(telemetry.Success.Value);

                // W3C compatible-Id ( should go away when W3C is implemented in .NET https://github.com/dotnet/corefx/issues/30331 TODO)
                Assert.AreEqual(32, telemetry.Context.Operation.Id.Length);
                Assert.IsTrue(Regex.Match(telemetry.Context.Operation.Id, @"[a-z][0-9]").Success);
                // end of workaround test

                Assert.AreEqual("eventhubname.servicebus.windows.net", telemetry.Properties["peer.hostname"]);
                Assert.AreEqual("ehname", telemetry.Properties["eh.event_hub_name"]);
                Assert.AreEqual("SomePartitionKeyHere", telemetry.Properties["eh.partition_key"]);
                Assert.AreEqual("EventHubClient1(ehname)", telemetry.Properties["eh.client_id"]);
            }
        }

        [TestMethod]
        public void EventHubsSuccessfulSendIsHandledWithExternalParent()
        {
            using (var module = new DependencyTrackingTelemetryModule())
            using (DiagnosticListener listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            {
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);
                var telemetry = this.TrackOperation<DependencyTelemetry>(listener, "Microsoft.Azure.EventHubs.Send",
                    TaskStatus.RanToCompletion, "parent");

                Assert.IsNotNull(telemetry);
                Assert.AreEqual("Send", telemetry.Name);
                Assert.AreEqual(RemoteDependencyConstants.AzureEventHubs, telemetry.Type);
                Assert.AreEqual("sb://eventhubname.servicebus.windows.net/ | ehname", telemetry.Target);
                Assert.IsTrue(telemetry.Success.Value);

                Assert.AreEqual("parent", telemetry.Context.Operation.ParentId);
                Assert.AreEqual("parent", telemetry.Context.Operation.Id);

                Assert.AreEqual("eventhubname.servicebus.windows.net", telemetry.Properties["peer.hostname"]);
                Assert.AreEqual("ehname", telemetry.Properties["eh.event_hub_name"]);
                Assert.AreEqual("SomePartitionKeyHere", telemetry.Properties["eh.partition_key"]);
                Assert.AreEqual("EventHubClient1(ehname)", telemetry.Properties["eh.client_id"]);
            }
        }

        [TestMethod]
        public void EventHubsFailedSendIsHandled()
        {
            using (var module = new DependencyTrackingTelemetryModule())
            using (var listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            {
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);

                Activity parentActivity = new Activity("parent").AddBaggage("k1", "v1").Start();
                var telemetry = this.TrackOperation<DependencyTelemetry>(listener, "Microsoft.Azure.EventHubs.Send",
                    TaskStatus.Faulted);

                Assert.IsNotNull(telemetry);
                Assert.AreEqual("Send", telemetry.Name);
                Assert.AreEqual(RemoteDependencyConstants.AzureEventHubs, telemetry.Type);
                Assert.AreEqual("sb://eventhubname.servicebus.windows.net/ | ehname", telemetry.Target);
                Assert.IsFalse(telemetry.Success.Value);

                Assert.AreEqual(parentActivity.Id, telemetry.Context.Operation.ParentId);
                Assert.AreEqual(parentActivity.RootId, telemetry.Context.Operation.Id);
                Assert.AreEqual("v1", telemetry.Properties["k1"]);
                Assert.AreEqual("eventhubname.servicebus.windows.net", telemetry.Properties["peer.hostname"]);
                Assert.AreEqual("ehname", telemetry.Properties["eh.event_hub_name"]);
                Assert.AreEqual("SomePartitionKeyHere", telemetry.Properties["eh.partition_key"]);
                Assert.AreEqual("EventHubClient1(ehname)", telemetry.Properties["eh.client_id"]);
            }
        }

        [TestMethod]
        public void EventHubsSendExceptionsAreIgnored()
        {
            using (var module = new DependencyTrackingTelemetryModule())
            using (var listener = new DiagnosticListener("Microsoft.Azure.EventHubs"))
            {
                this.configuration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
                module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
                module.Initialize(this.configuration);

                Activity parentActivity = new Activity("parent").AddBaggage("k1", "v1").Start();
                if (listener.IsEnabled("Microsoft.Azure.EventHubs.Send.Exception"))
                {
                    listener.Write("Microsoft.Azure.EventHubs.Send.Exception", new { Exception = new Exception("123") });
                }

                Assert.IsFalse(this.sentItems.Any());
            }
        }

        private T TrackOperation<T>(DiagnosticListener listener, string activityName, TaskStatus status, string parentId = null) where T : OperationTelemetry
        {
            Activity activity = null;
            int itemCountBefore = this.sentItems.Count;

            if (listener.IsEnabled(activityName))
            {
                activity = new Activity(activityName);
                activity.AddTag("peer.hostname", "eventhubname.servicebus.windows.net");
                activity.AddTag("eh.event_hub_name", "ehname");
                activity.AddTag("eh.partition_key", "SomePartitionKeyHere");
                activity.AddTag("eh.client_id", "EventHubClient1(ehname)");

                if (Activity.Current == null && parentId != null)
                {
                    activity.SetParentId(parentId);
                }

                if (listener.IsEnabled(activityName + ".Start"))
                {
                    listener.StartActivity(
                        activity,
                        new
                        {
                            Entity = "ehname",
                            Endpoint = new Uri("sb://eventhubname.servicebus.windows.net/"),
                            PartitionKey = "SomePartitionKeyHere"
                        });
                }
                else
                {
                    activity.Start();
                }
            }

            if (activity != null)
            {
                listener.StopActivity(
                    activity,
                    new
                    {
                        Entity = "ehname",
                        Endpoint = new Uri("sb://eventhubname.servicebus.windows.net/"),
                        PartitionKey = "SomePartitionKeyHere",
                        Status = status
                    });

                // a single new telemetry item was added
                Assert.AreEqual(itemCountBefore + 1, this.sentItems.Count);
                return this.sentItems.Last() as T;
            }

            // no new telemetry items were added
            Assert.AreEqual(itemCountBefore, this.sentItems.Count);
            return null;
        }
    }
}