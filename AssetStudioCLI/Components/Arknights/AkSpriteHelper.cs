using Arknights.PortraitSpriteMono;
using AssetStudio;
using AssetStudioCLI;
using AssetStudioCLI.Options;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;

namespace Arknights
{
    internal static class AkSpriteHelper
    {
        public static Texture2D TryFindAlphaTex(AssetItem assetItem, AvgSprite avgSprite, bool isAvgSprite)
        {
            Sprite m_Sprite = (Sprite)assetItem.Asset;
            var imgType = "arts/characters";
            if (m_Sprite.m_RD.alphaTexture.m_PathID == 0)
            {
                if (isAvgSprite)
                {
                    if (avgSprite?.FullAlphaTexture != null)
                        return avgSprite.FullAlphaTexture;

                    imgType = "avg/characters";  //since the avg hub was not found for some reason, let's try to find alpha tex by name
                }
                var spriteFullName = Path.GetFileNameWithoutExtension(assetItem.Container);
                foreach (var item in Studio.loadedAssetsList)
                {
                    if (item.Type == ClassIDType.Texture2D)
                    {
                        if (item.Container.Contains(imgType) && item.Container.Contains($"illust_{m_Sprite.m_Name}_material") && item.Text.Contains("[alpha]"))
                            return (Texture2D)item.Asset;
                        if (item.Container.Contains(imgType) && item.Container.Contains(spriteFullName) && item.Text == $"{m_Sprite.m_Name}[alpha]")
                            return (Texture2D)item.Asset;
                    }
                }
            }
            return null;
        }

        public static Image<Bgra32> AkGetImage(this Sprite m_Sprite, AvgSprite avgSprite = null, SpriteMaskMode spriteMaskMode = SpriteMaskMode.On)
        {
            if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D) && m_Sprite.m_RD.alphaTexture.TryGet(out var m_AlphaTexture2D) && spriteMaskMode != SpriteMaskMode.Off)
            {
                Image<Bgra32> tex;
                Image<Bgra32> alphaTex;

                if (avgSprite != null && avgSprite.IsHubParsed)
                {
                    alphaTex = m_AlphaTexture2D.ConvertToImage(true);
                    if (avgSprite.IsFaceSprite)
                    {
                        var faceImage = m_Texture2D.ConvertToImage(true);
                        var faceAlpha = avgSprite.FaceSpriteAlphaTexture.ConvertToImage(true);
                        if (new Size(faceImage.Width, faceImage.Height) != avgSprite.FaceSize)
                        {
                            faceImage.Mutate(x => x.Resize(new ResizeOptions { Size = avgSprite.FaceSize, Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Stretch }));
                            faceAlpha.Mutate(x => x.Resize(new ResizeOptions { Size = avgSprite.FaceSize, Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Stretch }));
                        }
                        tex = avgSprite.FullTexture.ConvertToImage(true);
                        tex.Mutate(x => x.DrawImage(faceImage, avgSprite.FacePos, opacity: 1f));
                        alphaTex.Mutate(x => x.DrawImage(faceAlpha, avgSprite.FacePos, opacity: 1f));
                    }
                    else
                    {
                        tex = m_Texture2D.ConvertToImage(true);
                    }
                }
                else
                {
                    tex = CutImage(m_Texture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
                    alphaTex = CutImage(m_AlphaTexture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
                }
                tex.ApplyRGBMask(alphaTex);
                return tex;
            }
            else if (m_Sprite.m_RD.texture.TryGet(out m_Texture2D) && avgSprite != null && avgSprite.IsHubParsed)
            {
                if (!avgSprite.IsFaceSprite)
                {
                    return m_Texture2D.ConvertToImage(true);
                }

                var faceImage = m_Texture2D.ConvertToImage(true);
                var tex = avgSprite.FullTexture.ConvertToImage(true);
                if (new Size(faceImage.Width, faceImage.Height) != avgSprite.FaceSize)
                {
                    faceImage.Mutate(x => x.Resize(new ResizeOptions { Size = avgSprite.FaceSize, Sampler = KnownResamplers.Lanczos3, Mode = ResizeMode.Stretch }));
                }
                tex.Mutate(x => x.DrawImage(faceImage, avgSprite.FacePos, opacity: 1f));

                return tex;
            }
            else if (m_Sprite.m_RD.texture.TryGet(out m_Texture2D))
            {
                return CutImage(m_Texture2D.ConvertToImage(false), m_Sprite.m_RD.textureRect, m_Sprite.m_RD.downscaleMultiplier);
            }

            return null;
        }

        public static Image<Bgra32> AkGetImage(this PortraitSprite portraitSprite, SpriteMaskMode spriteMaskMode = SpriteMaskMode.On)
        {
            if (portraitSprite.Texture != null && portraitSprite.AlphaTexture != null)
            {
                var tex = CutImage(portraitSprite.Texture.ConvertToImage(false), portraitSprite.TextureRect, portraitSprite.DownscaleMultiplier, portraitSprite.Rotate);

                if (spriteMaskMode == SpriteMaskMode.Off)
                {
                    return tex;
                }
                else
                {
                    var alphaTex = CutImage(portraitSprite.AlphaTexture.ConvertToImage(false), portraitSprite.TextureRect, portraitSprite.DownscaleMultiplier, portraitSprite.Rotate);
                    tex.ApplyRGBMask(alphaTex);
                    return tex;
                }
            }

            return null;
        }

        public static List<PortraitSprite> GeneratePortraits(AssetItem asset)
        {
            var portraits = new List<PortraitSprite>();

            var portraitsDict = ((MonoBehaviour)asset.Asset).ToType();
            if (portraitsDict == null)
            {
                Logger.Warning("Portraits MonoBehaviour is not readable.");
                return portraits;
            }
            var portraitsJson = JsonConvert.SerializeObject(portraitsDict);
            var portraitsData = JsonConvert.DeserializeObject<PortraitSpriteConfig>(portraitsJson);

            if (portraitsData._sprites.Length == 0)
                return portraits;

            var atlasTex = (Texture2D)Studio.loadedAssetsList.Find(x => x.m_PathID == portraitsData._atlas.Texture.m_PathID).Asset;
            var atlasAlpha = (Texture2D)Studio.loadedAssetsList.Find(x => x.m_PathID == portraitsData._atlas.Alpha.m_PathID).Asset;

            foreach (var portraitData in portraitsData._sprites)
            {
                var portraitSprite = new PortraitSprite()
                {
                    Name = portraitData.Name,
                    AssetsFile = atlasTex.assetsFile,
                    Container = asset.Container,
                    Texture = atlasTex,
                    AlphaTexture = atlasAlpha,
                    TextureRect = new Rectf(portraitData.Rect.X, portraitData.Rect.Y, portraitData.Rect.W, portraitData.Rect.H),
                    Rotate = portraitData.Rotate,
                };
                portraits.Add(portraitSprite);
            }

            return portraits;
        }

        private static void ApplyRGBMask(this Image<Bgra32> tex, Image<Bgra32> texMask)
        {
            using (texMask)
            {
                bool resized = false;
                if (tex.Width != texMask.Width || tex.Height != texMask.Height)
                {
                    texMask.Mutate(x => x.Resize(tex.Width, tex.Height, CLIOptions.o_akAlphaTexResampler.Value));
                    resized = true;
                }

                var invGamma = 1.0 / (1.0 + CLIOptions.o_akShadowGamma.Value / 10.0);
                if (CLIOptions.akResizedOnly && !resized)
                {
                    invGamma = 1.0;
                }

                tex.ProcessPixelRows(texMask, (sourceTex, targetTexMask) =>
                {
                    for (int y = 0; y < texMask.Height; y++)
                    {
                        var texRow = sourceTex.GetRowSpan(y);
                        var maskRow = targetTexMask.GetRowSpan(y);
                        for (int x = 0; x < maskRow.Length; x++)
                        {
                            var grayscale = (maskRow[x].R + maskRow[x].G + maskRow[x].B) / 3.0;
                            if (invGamma != 1.0)
                            {
                                grayscale = 255 - Math.Pow((255 - grayscale) / 255, invGamma) * 255;
                            }
                            texRow[x].A = (byte)grayscale;
                        }
                    }
                });
            }
        }

        private static Image<Bgra32> CutImage(Image<Bgra32> originalImage, Rectf textureRect, float downscaleMultiplier, bool rotate = false)
        {
            if (originalImage != null)
            {
                if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                {
                    var newSize = (Size)(new Size(originalImage.Width, originalImage.Height) / downscaleMultiplier);
                    originalImage.Mutate(x => x.Resize(newSize, KnownResamplers.Lanczos3, compand: true));
                }
                var rectX = (int)Math.Floor(textureRect.x);
                var rectY = (int)Math.Floor(textureRect.y);
                var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
                var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
                rectRight = Math.Min(rectRight, originalImage.Width);
                rectBottom = Math.Min(rectBottom, originalImage.Height);
                var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
                var spriteImage = originalImage.Clone(x => x.Crop(rect));
                originalImage.Dispose();
                if (rotate)
                {
                    spriteImage.Mutate(x => x.Rotate(RotateMode.Rotate270));
                }
                spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));

                return spriteImage;
            }

            return null;
        }
    }
}
