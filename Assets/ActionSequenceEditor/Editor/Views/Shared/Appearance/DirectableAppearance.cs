using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ActionSequenceEditor
{
    // 根据业务元数据解析标题图标和颜色，不缓存作者对象。
    public static class DirectableAppearance
    {
        private static readonly Dictionary<Type, Texture2D> _iconDictionary = new Dictionary<Type, Texture2D>();

        public static Texture2D GetIcon(this IDirectable track) => GetIcon(track.GetType());

        // 被动标题仅持有类型身份，图标解析不要求拿到作者对象。
        public static Texture2D GetIcon(Type type)
        {
            if (_iconDictionary.TryGetValue(type, out var icon))
            {
                return icon;
            }

            var att = type.RTGetAttribute<ShowIconAttribute>(true);

            if (att != null)
            {
                if (att.texture != null)
                {
                    icon = att.texture;
                }
                else if (!string.IsNullOrEmpty(att.iconPath))
                {
                    if (att.iconPath.StartsWith("Assets/"))
                    {
                        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(att.iconPath);
                    }
                    else
                    {
                        icon = Resources.Load(att.iconPath) as Texture2D;
                    }
                }

                if (icon == null && !string.IsNullOrEmpty(att.iconPath))
                {
                    icon = EditorGUIUtility.FindTexture(att.iconPath);
                }

                if (icon == null && att.fromType != null)
                {
                    icon = AssetPreview.GetMiniTypeThumbnail(att.fromType);
                }
            }

            if (icon != null)
            {
                _iconDictionary[type] = icon;
            }

            return icon;
        }

        public static Color GetColor(this IDirectable track) => DirectableTypeInfo.For(track.GetType()).Color;
        public static string GetName(this IDirectable track) => DirectableTypeInfo.For(track.GetType()).Name;
    }
}