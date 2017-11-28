﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LLGfx;
using LLGfx.Effects;
using OpenSage.Content.Util;
using OpenSage.Data;
using OpenSage.Data.W3d;
using OpenSage.Graphics;
using OpenSage.Graphics.Animation;
using OpenSage.Graphics.Effects;
using OpenSage.Mathematics;

namespace OpenSage.Content
{
    internal sealed class ModelLoader : ContentLoader<Model>
    {
        protected override Model LoadEntry(FileSystemEntry entry, ContentManager contentManager)
        {
            var w3dFile = W3dFile.FromFileSystemEntry(entry);

            var w3dHierarchy = w3dFile.Hierarchy;
            if (w3dFile.HLod != null && w3dHierarchy == null)
            {
                // Load referenced hierarchy.
                var hierarchyFileName = w3dFile.HLod.Header.HierarchyName + ".W3D";
                var hierarchyFilePath = Path.Combine(Path.GetDirectoryName(w3dFile.FilePath), hierarchyFileName);
                var hierarchyFileEntry = contentManager.FileSystem.GetFile(hierarchyFilePath);
                var hierarchyFile = W3dFile.FromFileSystemEntry(hierarchyFileEntry);
                w3dHierarchy = hierarchyFile.Hierarchy;
            }

            return CreateModel(
                contentManager,
                w3dFile,
                w3dHierarchy);
        }

        private static Model CreateModel(
            ContentManager contentManager,
            W3dFile w3dFile,
            W3dHierarchyDef w3dHierarchy)
        {
            ModelBone[] bones;
            if (w3dHierarchy != null)
            {
                if (w3dHierarchy.Pivots.Length > ModelMesh.MaxBones)
                {
                    throw new NotSupportedException();
                }

                bones = new ModelBone[w3dHierarchy.Pivots.Length];

                for (var i = 0; i < w3dHierarchy.Pivots.Length; i++)
                {
                    var pivot = w3dHierarchy.Pivots[i];

                    var parent = pivot.ParentIdx == -1
                        ? null
                        : bones[pivot.ParentIdx];

                    bones[i] = new ModelBone(
                        i,
                        pivot.Name,
                        parent,
                        pivot.Translation,
                        pivot.Rotation);
                }
            }
            else
            {
                bones = new ModelBone[1];
                bones[0] = new ModelBone(0, null, null, Vector3.Zero, Quaternion.Identity);
            }

            //BoundingSphere boundingSphere = default(BoundingSphere);

            var meshes = new ModelMesh[w3dFile.Meshes.Count];

            for (var i = 0; i < w3dFile.Meshes.Count; i++)
            {
                var w3dMesh = w3dFile.Meshes[i];

                ModelBone bone;
                if (w3dFile.HLod != null)
                {
                    var hlodSubObject = w3dFile.HLod.Lods[0].SubObjects.Single(x => x.Name == w3dMesh.Header.ContainerName + "." + w3dMesh.Header.MeshName);
                    bone = bones[(int) hlodSubObject.BoneIndex];
                }
                else
                {
                    bone = bones[0];
                }

                meshes[i] = CreateModelMesh(
                    contentManager,
                    w3dMesh,
                    bone,
                    bones.Length);

                //var meshBoundingSphere = mesh.BoundingSphere.Transform(bone.Transform);

                //boundingSphere = (i == 0)
                //    ? meshBoundingSphere
                //    : BoundingSphere.CreateMerged(boundingSphere, meshBoundingSphere);
            }

            var animations = new Animation[w3dFile.Animations.Count + w3dFile.CompressedAnimations.Count];
            for (var i = 0; i < w3dFile.Animations.Count; i++)
            {
                animations[i] = CreateAnimation(w3dFile.Animations[i]);
            }
            for (var i = 0; i < w3dFile.CompressedAnimations.Count; i++)
            {
                animations[w3dFile.Animations.Count + i] = CreateAnimation(w3dFile.CompressedAnimations[i]);
            }

            return new Model(
                bones,
                meshes,
                animations);
        }

        private static ModelMesh CreateModelMesh(
            ContentManager contentManager,
            W3dMesh w3dMesh,
            ModelBone parentBone,
            int numBones)
        {
            var materialPasses = new ModelMeshMaterialPass[w3dMesh.MaterialPasses.Length];
            for (var i = 0; i < materialPasses.Length; i++)
            {
                materialPasses[i] = CreateModelMeshMaterialPass(
                    contentManager,
                    w3dMesh,
                    w3dMesh.MaterialPasses[i]);
            }

            var shadingConfigurations = new ShadingConfiguration[w3dMesh.Shaders.Length];
            for (var i = 0; i < shadingConfigurations.Length; i++)
            {
                shadingConfigurations[i] = CreateShadingConfiguration(w3dMesh.Shaders[i]);
            }

            var boundingBox = new BoundingBox(
                w3dMesh.Header.Min,
                w3dMesh.Header.Max);

            var isSkinned = (w3dMesh.Header.Attributes & W3dMeshFlags.GeometryTypeMask) == W3dMeshFlags.GeometryTypeSkin;
            var cameraOriented = (w3dMesh.Header.Attributes & W3dMeshFlags.GeometryTypeMask) == W3dMeshFlags.GeometryTypeCameraOriented;

            return new ModelMesh(
                contentManager.GraphicsDevice,
                w3dMesh.Header.MeshName,
                CreateVertices(w3dMesh, isSkinned),
                CreateIndices(w3dMesh),
                CreateMaterials(w3dMesh),
                materialPasses,
                shadingConfigurations,
                isSkinned,
                parentBone,
                (uint) numBones,
                boundingBox,
                w3dMesh.Header.Attributes.HasFlag(W3dMeshFlags.Hidden),
                cameraOriented);
        }

        private static ShadingConfiguration CreateShadingConfiguration(W3dShader w3dShader)
        {
            return new ShadingConfiguration
            {
                DiffuseLightingType = w3dShader.PrimaryGradient.ToDiffuseLightingType(),
                SpecularEnabled = w3dShader.SecondaryGradient == W3dShaderSecondaryGradient.Enable,
                TexturingEnabled = w3dShader.Texturing == W3dShaderTexturing.Enable,
                SecondaryTextureColorBlend = w3dShader.DetailColorFunc.ToSecondaryTextureBlend(),
                SecondaryTextureAlphaBlend = w3dShader.DetailAlphaFunc.ToSecondaryTextureBlend(),
                AlphaTest = w3dShader.AlphaTest == W3dShaderAlphaTest.Enable
            };
        }

        private static VertexMaterial[] CreateMaterials(W3dMesh w3dMesh)
        {
            var vertexMaterials = new VertexMaterial[w3dMesh.Materials.Length];

            for (var i = 0; i < w3dMesh.Materials.Length; i++)
            {
                var w3dMaterial = w3dMesh.Materials[i];
                var w3dVertexMaterial = w3dMaterial.VertexMaterialInfo;

                vertexMaterials[i] = w3dVertexMaterial.ToVertexMaterial(w3dMaterial);
            }

            return vertexMaterials;
        }

        private static Texture CreateTexture(
            ContentManager contentManager,
            W3dMesh w3dMesh,
            uint? textureIndex)
        {
            if (textureIndex == null)
            {
                return null;
            }

            var w3dTexture = w3dMesh.Textures[(int) textureIndex];

            if (w3dTexture.TextureInfo != null && w3dTexture.TextureInfo.FrameCount != 1)
            {
                throw new NotImplementedException();
            }

            var w3dTextureFilePath = Path.Combine("Art", "Textures", w3dTexture.Name);

            var texture = contentManager.Load<Texture>(w3dTextureFilePath, fallbackToPlaceholder: false);
            if (texture == null)
            {
                w3dTextureFilePath = Path.Combine("Art", "CompiledTextures", w3dTexture.Name.Substring(0, 2), w3dTexture.Name);
                texture = contentManager.Load<Texture>(w3dTextureFilePath);
            }

            return texture;
        }

        private static MeshVertex.Basic[] CreateVertices(
            W3dMesh w3dMesh,
            bool isSkinned)
        {
            var numVertices = (uint) w3dMesh.Vertices.Length;
            var vertices = new MeshVertex.Basic[numVertices];

            for (var i = 0; i < numVertices; i++)
            {
                vertices[i] = new MeshVertex.Basic
                {
                    Position = w3dMesh.Vertices[i],
                    Normal = w3dMesh.Normals[i],
                    BoneIndex = isSkinned
                        ? w3dMesh.Influences[i].BoneIndex
                        : 0u
                };
            }

            return vertices;
        }

        private static ushort[] CreateIndices(W3dMesh w3dMesh)
        {
            var numIndices = (uint) w3dMesh.Triangles.Length * 3;
            var indices = new ushort[numIndices];

            var indexIndex = 0;
            foreach (var triangle in w3dMesh.Triangles)
            {
                indices[indexIndex++] = (ushort) triangle.VIndex0;
                indices[indexIndex++] = (ushort) triangle.VIndex1;
                indices[indexIndex++] = (ushort) triangle.VIndex2;
            }

            return indices;
        }

        // One ModelMeshMaterialPass for each W3D_CHUNK_MATERIAL_PASS
        private static ModelMeshMaterialPass CreateModelMeshMaterialPass(
            ContentManager contentManager,
            W3dMesh w3dMesh,
            W3dMaterialPass w3dMaterialPass)
        {
            var hasTextureStage0 = w3dMaterialPass.TextureStages.Count > 0;
            var textureStage0 = hasTextureStage0
                ? w3dMaterialPass.TextureStages[0]
                : null;

            var hasTextureStage1 = w3dMaterialPass.TextureStages.Count > 1;
            var textureStage1 = hasTextureStage1
                ? w3dMaterialPass.TextureStages[1]
                : null;

            var numTextureStages = hasTextureStage0 && hasTextureStage1
                ? 2u
                : hasTextureStage0 ? 1u : 0u;

            var texCoords = new MeshVertex.TexCoords[w3dMesh.Header.NumVertices];

            if (hasTextureStage0 || (w3dMaterialPass.ShaderMaterialId != null && w3dMaterialPass.TexCoords != null))
            {
                for (var i = 0; i < texCoords.Length; i++)
                {
                    // TODO: What to do when this is null?
                    if (textureStage0.TexCoords != null)
                    {
                        texCoords[i].UV0 = textureStage0.TexCoords[i];
                    }
                    else if (w3dMaterialPass.ShaderMaterialId != null && w3dMaterialPass.TexCoords != null)
                    {
                        texCoords[i].UV0 = w3dMaterialPass.TexCoords[i];
                    }

                    if (hasTextureStage1)
                    {
                        texCoords[i].UV1 = textureStage1.TexCoords[i];
                    }
                }
            }

            var materialIndices = w3dMaterialPass.VertexMaterialIds;
            if (materialIndices.Length == 1)
            {
                var materialID = materialIndices[0];
                materialIndices = new uint[w3dMesh.Header.NumVertices];
                for (var i = 0; i < w3dMesh.Header.NumVertices; i++)
                {
                    materialIndices[i] = materialID;
                }
            }

            var meshParts = new List<ModelMeshPart>();

            // Optimisation for a fairly common case.
            if (w3dMaterialPass.ShaderIds.Length == 1
                && w3dMaterialPass.TextureStages.Count == 1
                && w3dMaterialPass.TextureStages[0].TextureIds.Length == 1)
            {
                meshParts.Add(CreateModelMeshPart(
                    contentManager,
                    0, 
                    w3dMesh.Header.NumTris * 3,
                    w3dMesh,
                    w3dMaterialPass.ShaderIds[0],
                    w3dMaterialPass.TextureStages[0].TextureIds[0],
                    0));
            }
            else
            {
                // Expand ShaderIds and TextureIds, if they have a single entry
                // (which means same ID for all faces)

                IEnumerable<uint?> getExpandedTextureIds(uint?[] ids)
                {
                    if (ids == null)
                    {
                        for (var i = 0; i < w3dMesh.Header.NumTris; i++)
                        {
                            yield return null;
                        }
                    }
                    else if (ids.Length == 1)
                    {
                        var result = ids[0];
                        for (var i = 0; i < w3dMesh.Header.NumTris; i++)
                        {
                            yield return result;
                        }
                    }
                    else
                    {
                        foreach (var id in ids)
                        {
                            yield return id;
                        }
                    }
                }

                IEnumerable<uint> getExpandedShaderIds(uint[] ids)
                {
                    if (ids.Length == 1)
                    {
                        var result = ids[0];
                        for (var i = 0; i < w3dMesh.Header.NumTris; i++)
                        {
                            yield return result;
                        }
                    }
                    else
                    {
                        foreach (var id in ids)
                        {
                            yield return id;
                        }
                    }
                }

                var combinedIds = getExpandedShaderIds(w3dMaterialPass.ShaderIds)
                    .Zip(getExpandedTextureIds(textureStage0?.TextureIds), (x, y) => new { ShaderId = x, TextureIndex0 = y })
                    .Zip(getExpandedTextureIds(textureStage1?.TextureIds), (x, y) => new { x.ShaderId, x.TextureIndex0, TextureIndex1 = y });

                var combinedId = combinedIds.First();
                var startIndex = 0u;
                var indexCount = 0u;

                foreach (var newCombinedId in combinedIds)
                {
                    if (combinedId != newCombinedId)
                    {
                        meshParts.Add(CreateModelMeshPart(
                            contentManager,
                            startIndex,
                            indexCount,
                            w3dMesh,
                            combinedId.ShaderId,
                            combinedId.TextureIndex0,
                            combinedId.TextureIndex1));

                        startIndex = startIndex + indexCount;
                        indexCount = 0;
                    }

                    combinedId = newCombinedId;

                    indexCount += 3;
                }

                if (indexCount > 0)
                {
                    meshParts.Add(CreateModelMeshPart(
                        contentManager,
                        startIndex,
                        indexCount,
                        w3dMesh,
                        combinedId.ShaderId,
                        combinedId.TextureIndex0,
                        combinedId.TextureIndex1));
                }
            }

            return new ModelMeshMaterialPass(
                contentManager.GraphicsDevice,
                numTextureStages,
                texCoords,
                materialIndices,
                meshParts);
        }

        // One ModelMeshPart for each unique shader in a W3D_CHUNK_MATERIAL_PASS.
        private static ModelMeshPart CreateModelMeshPart(
            ContentManager contentManager,
            uint startIndex,
            uint indexCount,
            W3dMesh w3dMesh,
            uint shaderID,
            uint? textureIndex0,
            uint? textureIndex1)
        {
            var w3dShader = w3dMesh.Shaders[shaderID];

            var rasterizerState = RasterizerStateDescription.CullBackSolid;
            rasterizerState.CullMode = w3dMesh.Header.Attributes.HasFlag(W3dMeshFlags.TwoSided)
                ? CullMode.None
                : CullMode.CullBack;

            var depthState = DepthStencilStateDescription.Default;
            depthState.IsDepthEnabled = true;
            depthState.IsDepthWriteEnabled = w3dShader.DepthMask == W3dShaderDepthMask.WriteEnable;
            depthState.DepthComparison = w3dShader.DepthCompare.ToComparison();

            var blendState = BlendStateDescription.Opaque;
            blendState.Enabled = w3dShader.SrcBlend != W3dShaderSrcBlendFunc.One
                || w3dShader.DestBlend != W3dShaderDestBlendFunc.Zero;
            blendState.SourceBlend = w3dShader.SrcBlend.ToBlend();
            blendState.SourceAlphaBlend = w3dShader.SrcBlend.ToBlend();
            blendState.DestinationBlend = w3dShader.DestBlend.ToBlend(false);
            blendState.DestinationAlphaBlend = w3dShader.DestBlend.ToBlend(true);

            var pipelineStateHandle = new EffectPipelineState(
                rasterizerState,
                depthState,
                blendState)
                .GetHandle();

            return new ModelMeshPart(
                startIndex,
                indexCount,
                shaderID,
                pipelineStateHandle,
                CreateTexture(contentManager, w3dMesh, textureIndex0),
                CreateTexture(contentManager, w3dMesh, textureIndex1));
        }

        private static Animation CreateAnimation(W3dAnimation w3dAnimation)
        {
            var name = w3dAnimation.Header.Name;
            var duration = TimeSpan.FromSeconds(w3dAnimation.Header.NumFrames / (double) w3dAnimation.Header.FrameRate);

            var channels = w3dAnimation.Channels
                .Where(x => x.ChannelType != W3dAnimationChannelType.UnknownBfme) // Don't know what this channel means.
                .ToList();

            var clips = new AnimationClip[channels.Count + w3dAnimation.BitChannels.Count];

            for (var i = 0; i < channels.Count; i++)
            {
                clips[i] = CreateAnimationClip(w3dAnimation, channels[i]);
            }

            for (var i = 0; i < w3dAnimation.BitChannels.Count; i++)
            {
                clips[channels.Count + i] = CreateAnimationClip(w3dAnimation, w3dAnimation.BitChannels[i]);
            }

            return new Animation(
                name,
                duration,
                clips);
        }

        private static Animation CreateAnimation(W3dCompressedAnimation w3dAnimation)
        {
            var name = w3dAnimation.Header.Name;
            var duration = TimeSpan.FromSeconds(w3dAnimation.Header.NumFrames / (double) w3dAnimation.Header.FrameRate);

            var timeCodedChannels = w3dAnimation.TimeCodedChannels
                .Where(x => x.ChannelType != W3dAnimationChannelType.UnknownBfme) // Don't know what this channel means.
                .ToList();

            var clips = new AnimationClip[timeCodedChannels.Count];

            for (var i = 0; i < timeCodedChannels.Count; i++)
            {
                clips[i] = CreateAnimationClip(w3dAnimation, timeCodedChannels[i]);
            }

            return new Animation(
                name,
                duration,
                clips);
        }

        private static AnimationClip CreateAnimationClip(W3dAnimation w3dAnimation, W3dAnimationChannel w3dChannel)
        {
            var bone = w3dChannel.Pivot;

            var data = w3dChannel.Data;
            var numKeyframes = data.GetLength(0);
            var keyframes = new Keyframe[numKeyframes];

            for (var i = 0; i < numKeyframes; i++)
            {
                var time = TimeSpan.FromSeconds((w3dChannel.FirstFrame + i) / (double) w3dAnimation.Header.FrameRate);
                keyframes[i] = CreateKeyframe(w3dChannel.ChannelType, time, ref data[i]);
            }

            return new AnimationClip(w3dChannel.ChannelType.ToAnimationClipType(), bone, keyframes);
        }

        private static Keyframe CreateKeyframe(W3dAnimationChannelType channelType, TimeSpan time, ref W3dAnimationChannelDatum datum)
        {
            return new Keyframe(time, CreateKeyframeValue(channelType, ref datum));
        }

        private static KeyframeValue CreateKeyframeValue(W3dAnimationChannelType channelType, ref W3dAnimationChannelDatum datum)
        {
            switch (channelType)
            {
                case W3dAnimationChannelType.Quaternion:
                    return new KeyframeValue { Quaternion = datum.Quaternion };

                case W3dAnimationChannelType.TranslationX:
                case W3dAnimationChannelType.TranslationY:
                case W3dAnimationChannelType.TranslationZ:
                    return new KeyframeValue { FloatValue = datum.FloatValue };

                default:
                    throw new NotImplementedException();
            }
        }

        private static AnimationClip CreateAnimationClip(W3dAnimation w3dAnimation, W3dBitChannel w3dChannel)
        {
            var bone = w3dChannel.Pivot;

            var data = w3dChannel.Data;

            var numKeyframes = data.GetLength(0);

            var totalKeyframes = numKeyframes;
            if (w3dChannel.FirstFrame != 0)
            {
                totalKeyframes++;
            }
            if (w3dChannel.LastFrame != w3dAnimation.Header.NumFrames - 1)
            {
                totalKeyframes++;
            }

            var keyframes = new Keyframe[totalKeyframes];

            var keyframeIndex = 0;
            if (w3dChannel.FirstFrame != 0)
            {
                keyframes[keyframeIndex++] = new Keyframe(TimeSpan.Zero, new KeyframeValue { BoolValue = w3dChannel.DefaultValue });
            }

            for (var i = 0; i < numKeyframes; i++)
            {
                var time = TimeSpan.FromSeconds((w3dChannel.FirstFrame + i) / (double) w3dAnimation.Header.FrameRate);

                switch (w3dChannel.ChannelType)
                {
                    case W3dBitChannelType.Visibility:
                        keyframes[keyframeIndex++] = new Keyframe(time, new KeyframeValue { BoolValue = data[i] });
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            if (w3dChannel.LastFrame != w3dAnimation.Header.NumFrames - 1)
            {
                var time = TimeSpan.FromSeconds((w3dChannel.LastFrame + 1) / (double) w3dAnimation.Header.FrameRate);
                keyframes[keyframeIndex++] = new Keyframe(time, new KeyframeValue { BoolValue = w3dChannel.DefaultValue });
            }

            return new AnimationClip(AnimationClipType.Visibility, bone, keyframes);
        }

        private static AnimationClip CreateAnimationClip(W3dCompressedAnimation w3dAnimation, W3dTimeCodedAnimationChannel w3dChannel)
        {
            var bone = w3dChannel.Pivot;

            var keyframes = new Keyframe[w3dChannel.NumTimeCodes];

            for (var i = 0; i < w3dChannel.NumTimeCodes; i++)
            {
                var timeCodedDatum = w3dChannel.Data[i];
                var time = TimeSpan.FromSeconds(timeCodedDatum.TimeCode / (double) w3dAnimation.Header.FrameRate);
                keyframes[i] = CreateKeyframe(w3dChannel.ChannelType, time, ref timeCodedDatum.Value);
            }

            return new AnimationClip(w3dChannel.ChannelType.ToAnimationClipType(), bone, keyframes);
        }
    }
}