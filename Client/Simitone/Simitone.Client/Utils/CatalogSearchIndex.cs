using FSO.Content;
using FSO.Content.Interfaces;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Text search over the buy catalog (roadmap 11). The TS1 catalog entries only carry the internal OBJD label as
    /// Name (TS1ObjectProvider never fills CatalogName), so the localised name and description are read from each
    /// object's catalog strings (CTSS, string 0 and 1), as the query panel does. Built once, on the first search,
    /// because it loads every catalog object's resource.
    /// Nothing new is authored: only existing names and descriptions are searched.
    /// </summary>
    public static class CatalogSearchIndex
    {
        public class Entry
        {
            public ObjectCatalogItem Item;
            public string Name;
            public string Description;
            public string Label;
        }

        private static List<Entry> Entries;

        public static List<Entry> All
        {
            get
            {
                if (Entries == null) Build();
                return Entries;
            }
        }

        private static void Build()
        {
            var list = new List<Entry>();
            var content = Content.Get();
            foreach (var item in content.WorldCatalog.All())
            {
                string name = null, desc = null;
                try
                {
                    var obj = content.WorldObjects.Get(item.GUID);
                    var ctss = obj?.Resource?.Get<CTSS>(obj.OBJ.CatalogStringsID);
                    name = ctss?.GetString(0);
                    desc = ctss?.GetString(1);
                }
                catch (Exception)
                {
                    //a broken custom object: fall back to its label.
                }
                list.Add(new Entry()
                {
                    Item = item,
                    Name = (name ?? item.Name ?? "").ToLowerInvariant(),
                    Description = (desc ?? "").ToLowerInvariant(),
                    Label = (item.Name ?? "").ToLowerInvariant()
                });
            }
            Entries = list;
        }

        /// <summary>
        /// Catalog items passing the filter whose name, description or label contain every word of the query. Names that
        /// start with the query come first, then other name matches, then description-only matches; cheaper first within
        /// each group (the catalog's own order).
        /// </summary>
        public static List<ObjectCatalogItem> Find(string query, Func<ObjectCatalogItem, bool> filter)
        {
            var words = (query ?? "").ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return new List<ObjectCatalogItem>();
            var whole = string.Join(" ", words);
            return All
                .Where(x => filter(x.Item) && words.All(w => x.Name.Contains(w) || x.Description.Contains(w) || x.Label.Contains(w)))
                .Select(x => new { x.Item, Rank = Rank(x, words, whole) })
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Item.Price)
                .Select(x => x.Item)
                .ToList();
        }

        public static int Rank(Entry entry, string[] words, string whole)
        {
            if (entry.Name.StartsWith(whole)) return 0;
            if (words.All(w => entry.Name.Contains(w))) return 1;
            if (words.Any(w => entry.Name.Contains(w))) return 2;
            return 3;
        }
    }
}
