﻿namespace IngestionService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class DataStorageInMemory
    {
        internal static Dictionary<string, List<string>>   itemsDictionary = new Dictionary<string, List<string>>();

        public DataStorageInMemory()
        {
        }

        public void SaveDataItem(
            string instrumentationKey,
            string data)
        {
            if (true == string.IsNullOrWhiteSpace(instrumentationKey))
            {
                throw new ArgumentNullException("instrumentationKey");
            }

            if (true == string.IsNullOrWhiteSpace("data"))
            {
                throw new ArgumentNullException("data");
            }

            List<string> items;
            if (itemsDictionary.TryGetValue(instrumentationKey, out items))
            {
                items.Add(data);
            }
            else
            {
                items = new List<string>();
                items.Add(data);
                itemsDictionary.Add(instrumentationKey, items);
            }
        }

        public IEnumerable<string> GetItemIds(
            string instrumentationKey)
        {
            List<string> items = null;
            if (itemsDictionary.TryGetValue(instrumentationKey, out items))
            {
                return items;
            }
            else
            {
                return new List<string>();
            }
        }
         
        public IEnumerable<string> DeleteItems(
            string instrumentationKey)
        {
            
            var deletedItems = new List<string>();
            List<string> items;
            if (itemsDictionary.TryGetValue(instrumentationKey, out items))
            {
                deletedItems.AddRange(items);
                items.Clear();
            }
            
            return deletedItems;
        }
    }
}