using System;
using System.Collections.Generic;
using System.Text;
using AssetStudio;

namespace CubismLive2DExtractor
{
    class CubismMotion3Converter
    {
        private Dictionary<uint, string> bonePathHash = new Dictionary<uint, string>();
        public List<ImportedKeyframedAnimation> AnimationList { get; protected set; } = new List<ImportedKeyframedAnimation>();

        public CubismMotion3Converter(GameObject rootGameObject, List<AnimationClip> animationClips)
        {
            var rootTransform = GetTransform(rootGameObject);
            CreateBonePathHash(rootTransform);
            ConvertAnimations(animationClips);
        }

        public CubismMotion3Converter(List<AnimationClip> animationClips, HashSet<string> partIds, HashSet<string> parameterIds)
        {
            CreateBonePathHash(partIds, pathType: "Parts/");
            CreateBonePathHash(parameterIds, pathType: "Parameters/");
            ConvertAnimations(animationClips);
        }

        private void ConvertAnimations(List<AnimationClip> animationClips)
        {
            foreach (var animationClip in animationClips)
            {
                var iAnim = new ImportedKeyframedAnimation();
                AnimationList.Add(iAnim);
                iAnim.Name = animationClip.m_Name;
                iAnim.SampleRate = animationClip.m_SampleRate;
                iAnim.Duration = animationClip.m_MuscleClip.m_StopTime;
                var m_Clip = animationClip.m_MuscleClip.m_Clip.data;
                var streamedFrames = m_Clip.m_StreamedClip.ReadData();
                var m_ClipBindingConstant = animationClip.m_ClipBindingConstant;
                for (int frameIndex = 1; frameIndex < streamedFrames.Count - 1; frameIndex++)
                {
                    var frame = streamedFrames[frameIndex];
                    for (int curveIndex = 0; curveIndex < frame.keyList.Length; curveIndex++)
                    {
                        ReadStreamedData(iAnim, m_ClipBindingConstant, frame.time, frame.keyList[curveIndex]);
                    }
                }
                var m_DenseClip = m_Clip.m_DenseClip;
                var streamCount = m_Clip.m_StreamedClip.curveCount;
                for (int frameIndex = 0; frameIndex < m_DenseClip.m_FrameCount; frameIndex++)
                {
                    var time = m_DenseClip.m_BeginTime + frameIndex / m_DenseClip.m_SampleRate;
                    var frameOffset = frameIndex * m_DenseClip.m_CurveCount;
                    for (int curveIndex = 0; curveIndex < m_DenseClip.m_CurveCount; curveIndex++)
                    {
                        var index = streamCount + curveIndex;
                        ReadCurveData(iAnim, m_ClipBindingConstant, (int)index, time, m_DenseClip.m_SampleArray, (int)frameOffset, curveIndex);
                    }
                }
                var m_ConstantClip = m_Clip.m_ConstantClip;
                var denseCount = m_Clip.m_DenseClip.m_CurveCount;
                var time2 = 0.0f;
                for (int i = 0; i < 2; i++)
                {
                    for (int curveIndex = 0; curveIndex < m_ConstantClip.data.Length; curveIndex++)
                    {
                        var index = streamCount + denseCount + curveIndex;
                        ReadCurveData(iAnim, m_ClipBindingConstant, (int)index, time2, m_ConstantClip.data, 0, curveIndex);
                    }
                    time2 = animationClip.m_MuscleClip.m_StopTime;
                }
                foreach (var m_Event in animationClip.m_Events)
                {
                    iAnim.Events.Add(new ImportedEvent
                    {
                        time = m_Event.time,
                        value = m_Event.data
                    });
                }

                if (iAnim.TrackList.Count == 0 || iAnim.Events.Count == 0)
                {
                    Logger.Warning($"[Motion Converter] \"{iAnim.Name}\" has {iAnim.TrackList.Count} tracks and {iAnim.Events.Count} event!.");
                }
            }
        }

        private void ReadStreamedData(ImportedKeyframedAnimation iAnim, AnimationClipBindingConstant m_ClipBindingConstant, float time, StreamedClip.StreamedCurveKey curveKey)
        {
            var binding = m_ClipBindingConstant.FindBinding(curveKey.index);
            GetLive2dPath(binding, out var target, out var boneName);
            if (string.IsNullOrEmpty(boneName))
            {
                Logger.Warning($"[Motion Converter] \"{iAnim.Name}\" read fail on binding {Array.IndexOf(m_ClipBindingConstant.genericBindings, binding)}");
                return;
            }

            var track = iAnim.FindTrack(boneName);
            track.Target = target;
            track.Curve.Add(new ImportedKeyframe<float>(time, curveKey.value, curveKey.inSlope, curveKey.outSlope, curveKey.coeff));
        }

        private void ReadCurveData(ImportedKeyframedAnimation iAnim, AnimationClipBindingConstant m_ClipBindingConstant, int index, float time, float[] data, int offset, int curveIndex)
        {
            var binding = m_ClipBindingConstant.FindBinding(index);
            GetLive2dPath(binding, out var target, out var boneName);
            if (string.IsNullOrEmpty(boneName))
            {
                Logger.Warning($"[Motion Converter] \"{iAnim.Name}\" read fail on binding {Array.IndexOf(m_ClipBindingConstant.genericBindings, binding)}");
                return;
            }

            var track = iAnim.FindTrack(boneName);
            track.Target = target;
            var value = data[curveIndex];
            track.Curve.Add(new ImportedKeyframe<float>(time, value, 0, 0, null));
        }

        private void GetLive2dPath(GenericBinding binding, out string target, out string id)
        {
            var path = binding.path;
            id = null;
            target = null;
            if (path != 0 && bonePathHash.TryGetValue(path, out var boneName))
            {
                var index = boneName.LastIndexOf('/');
                id = boneName.Substring(index + 1);
                target = boneName.Substring(0, index);
                if (target == "Parameters")
                {
                    target = "Parameter";
                }
                else if (target == "Parts")
                {
                    target = "PartOpacity";
                }
            }
            else if (binding.script.TryGet(out MonoScript script))
            {
                switch (script.m_ClassName)
                {
                    case "CubismRenderController":
                        target = "Model";
                        id = "Opacity";
                        break;
                    case "CubismEyeBlinkController":
                        target = "Model";
                        id = "EyeBlink";
                        break;
                    case "CubismMouthController":
                        target = "Model";
                        id = "LipSync";
                        break;
                }
            }
        }

        private Transform GetTransform(GameObject gameObject)
        {
            foreach (var m_Component in gameObject.m_Components)
            {
                if (m_Component.TryGet(out Transform m_Transform))
                {
                    return m_Transform;
                }
            }

            return null;
        }

        private void CreateBonePathHash(HashSet<string> ids, string pathType)
        {
            foreach (var id in ids)
            {
                var name = pathType + id;;
                bonePathHash[GetCRC(name)] = name;
                int index;
                while ((index = name.IndexOf("/", StringComparison.Ordinal)) >= 0)
                {
                    name = name.Substring(index + 1);
                    bonePathHash[GetCRC(name)] = name;
                }
            }
        }

        private void CreateBonePathHash(Transform m_Transform)
        {
            var name = GetTransformPath(m_Transform);
            bonePathHash[GetCRC(name)] = name;
            int index;
            while ((index = name.IndexOf("/", StringComparison.Ordinal)) >= 0)
            {
                name = name.Substring(index + 1);
                bonePathHash[GetCRC(name)] = name;
            }
            foreach (var pptr in m_Transform.m_Children)
            {
                if (pptr.TryGet(out var child))
                    CreateBonePathHash(child);
            }
        }

        private static uint GetCRC(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            return SevenZip.CRC.CalculateDigest(bytes, 0, (uint)bytes.Length);
        }

        private static string GetTransformPath(Transform transform)
        {
            transform.m_GameObject.TryGet(out var m_GameObject);
            if (transform.m_Father.TryGet(out var father))
            {
                return GetTransformPath(father) + "/" + m_GameObject.m_Name;
            }

            return m_GameObject.m_Name;
        }
    }
}
