using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor.Views
{
    // 共享资源发现，不持有窗口、选择或预览Editor。
    internal static class AssetPickerDiscovery
    {
        internal sealed class Entry
        {
            public string name, path;
            public Texture icon;
            public int id;
        }
        public static Entry[] Discover(string folder, string requiredType, List<int> allowedIds, Predicate<string> accepts)
        {
            var entries = new List<Entry>();
            if (allowedIds == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + requiredType, new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (accepts != null && !accepts(path)) continue;
                    entries.Add(new Entry { name = RelativeName(folder, path), path = path, icon = AssetDatabase.GetCachedIcon(path) });
                }
            }
            else
            {
                foreach (var id in allowedIds)
                {
                    var path = AssetDatabase.GetAssetPath(id);
                    if (accepts != null && !accepts(path)) continue;
                    var resource = EditorUtility.InstanceIDToObject(id);
                    var subAsset = AssetDatabase.IsSubAsset(id);
                    entries.Add(new Entry { name = subAsset ? resource.name : RelativeName(folder, path), path = path, id = id,
                        icon = subAsset ? AssetPreview.GetMiniThumbnail(resource) : AssetDatabase.GetCachedIcon(path) });
                }
            }
            return entries.ToArray();
        }
        private static string RelativeName(string folder, string path) => path.Substring(folder.Length, path.LastIndexOf('.') - folder.Length);
    }
}
