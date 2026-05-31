using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BpmTapTool
{
    public static class JsonHistoryHelper
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BpmHistory.json");
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static List<BpmHistoryItem> LoadAllHistory()
        {
            if (!File.Exists(FilePath)) return new List<BpmHistoryItem>();
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<BpmHistoryItem>>(json) ?? new List<BpmHistoryItem>();
            }
            catch { return new List<BpmHistoryItem>(); }
        }

        private static void SaveAllHistory(IEnumerable<BpmHistoryItem> items)
        {
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            File.WriteAllText(FilePath, json);
        }

        public static void AddHistory(BpmHistoryItem item)
        {
            var list = LoadAllHistory();
            int newId = list.Count == 0 ? 1 : list.Max(x => x.Id) + 1;
            item.Id = newId;
            list.Insert(0, item);
            SaveAllHistory(list);
        }

        public static void UpdateHistory(BpmHistoryItem updatedItem)
        {
            var list = LoadAllHistory();
            var index = list.FindIndex(x => x.Id == updatedItem.Id);
            if (index >= 0)
            {
                list[index] = updatedItem;
                SaveAllHistory(list);
            }
        }

        public static void DeleteHistory(int id)
        {
            var list = LoadAllHistory();
            list.RemoveAll(x => x.Id == id);
            SaveAllHistory(list);
        }
    }
}