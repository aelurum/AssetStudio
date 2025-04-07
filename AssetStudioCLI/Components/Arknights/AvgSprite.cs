using Arknights.AvgCharHubMono;
using AssetStudio;
using AssetStudioCLI;
using SixLabors.ImageSharp;
using System.Linq;
using System;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace Arknights
{
    internal class AvgSprite
    {
        public Texture2D FaceSpriteAlphaTexture { get; }
        public Texture2D FullTexture { get; }
        public Texture2D FullAlphaTexture { get; }
        public Point FacePos { get; }
        public Size FaceSize { get; }
        public string Alias { get; }
        public bool IsWholeBodySprite { get; }
        public bool IsFaceSprite { get; }
        public bool IsHubParsed { get; }

        private AvgSpriteConfig GetCurSpriteGroup(AvgSpriteConfigGroup spriteHubDataGrouped, long spriteItemID, string spriteName)
        {
            if (spriteHubDataGrouped.SpriteGroups.Length > 1)
            {
                if (!string.IsNullOrEmpty(spriteName))
                {
                    var groupFromName = int.TryParse(spriteName?.Substring(spriteName.IndexOf('$') + 1, 1), out int groupIndex);
                    if (groupFromName)
                    {
                        return spriteHubDataGrouped.SpriteGroups[groupIndex - 1];
                    }
                }
                return spriteHubDataGrouped.SpriteGroups.FirstOrDefault(x => x.Sprites.Any(y => y.Sprite.m_PathID == spriteItemID));
            }
            else
            {
                return spriteHubDataGrouped.SpriteGroups[0];
            }
        }

        private bool TryGetSpriteHub(AssetItem assetItem, out AvgSpriteConfig spriteHubData)
        {
            spriteHubData = null;
            var scriptAssets = Studio.loadedAssetsList.FindAll(x =>
                x.Type == ClassIDType.MonoBehaviour
                && x.Container == assetItem.Container);
            if (scriptAssets.Count == 0)
            {
                Logger.Warning("No MonoBehaviours were found.");
                return false;
            }

            OrderedDictionary spriteHubDict = null;
            var isGrouped = false;
            foreach (var scriptAsset in scriptAssets)
            {
                var scriptAssetDict = ((MonoBehaviour)scriptAsset.Asset).ToType();
                if (scriptAssetDict == null)
                {
                    Logger.Warning("MonoBehaviour is not readable.");
                    return false;
                }

                if (scriptAssetDict.Contains("spriteGroups"))
                {
                    spriteHubDict = scriptAssetDict;
                    isGrouped = true;
                    break;
                }
                if (scriptAssetDict.Contains("sprites"))
                {
                    spriteHubDict = scriptAssetDict;
                    break;
                }
            }

            if (spriteHubDict == null)
            {
                Logger.Warning("AVGCharacterSpriteHub is not readable.");
                return false;
            }

            var spriteHubJson = JsonConvert.SerializeObject(spriteHubDict);
            if (isGrouped)
            {
                var groupedSpriteHub = JsonConvert.DeserializeObject<AvgSpriteConfigGroup>(spriteHubJson);
                spriteHubData = GetCurSpriteGroup(groupedSpriteHub, assetItem.m_PathID, assetItem.Text);
            }
            else
            {
                spriteHubData = JsonConvert.DeserializeObject<AvgSpriteConfig>(spriteHubJson);
            }

            return true;
        }

        public AvgSprite(AssetItem assetItem)
        {
            if (TryGetSpriteHub(assetItem, out var spriteHubData))
            {
                IsHubParsed = spriteHubData?.Sprites.Length > 0;
            }
            if (IsHubParsed)
            {
                var curSpriteData = spriteHubData.Sprites.FirstOrDefault(x => x.Sprite.m_PathID == assetItem.m_PathID);

                if (curSpriteData == null)
                {
                    Logger.Warning($"Sprite \"{assetItem.Text}\" was not found in the avg sprite hub");
                    return;
                }

                Alias = curSpriteData.Alias;
                IsWholeBodySprite = curSpriteData.IsWholeBody;

                if (spriteHubData.FaceSize.X > 0 && spriteHubData.FaceSize.Y > 0)  //If face data exist
                {
                    var fullTexSpriteData = spriteHubData.Sprites.Last(); //Last sprite item in the list usually contains PathID of Sprite with full texture

                    var curSprite = (Sprite)assetItem.Asset;
                    IsFaceSprite = curSprite.m_Rect.width <= 256 && curSprite.m_Rect.height <= 256 && curSprite.m_PathID != fullTexSpriteData.Sprite.m_PathID;

                    var curSpriteAlphaID = curSpriteData.AlphaTex.m_PathID;
                    var curSpriteAlphaTex = (Texture2D)Studio.loadedAssetsList.Find(x => x.m_PathID == curSpriteAlphaID)?.Asset;
                    if (curSpriteAlphaTex != null)
                    {
                        FaceSpriteAlphaTexture = IsFaceSprite ? curSpriteAlphaTex : null;
                        fullTexSpriteData = IsFaceSprite ? fullTexSpriteData : curSpriteData;
                    }
                    var fullTexSpriteID = fullTexSpriteData.Sprite.m_PathID;
                    var fullTexAlphaID = fullTexSpriteData.AlphaTex.m_PathID;
                    var fullTexSprite = (Sprite)Studio.loadedAssetsList.Find(x => x.m_PathID == fullTexSpriteID).Asset;

                    FullTexture = fullTexSprite.m_RD.texture.TryGet(out var fullTex) ? fullTex : null;
                    FullAlphaTexture = (Texture2D)Studio.loadedAssetsList.Find(x => x.m_PathID == fullTexAlphaID)?.Asset;
                    FacePos = new Point((int)Math.Round(spriteHubData.FacePos.X), (int)Math.Round(spriteHubData.FacePos.Y));
                    FaceSize = new Size((int)Math.Round(spriteHubData.FaceSize.X), (int)Math.Round(spriteHubData.FaceSize.Y));
                }
                else
                {
                    FullAlphaTexture = (Texture2D)Studio.loadedAssetsList.Find(x => x.m_PathID == curSpriteData.AlphaTex.m_PathID).Asset;
                }
            }
        }
    }
}
