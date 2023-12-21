// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utilities.Encoding.OggVorbis.Editor
{
    internal static class OggVorbisEditorUtilities
    {
        [MenuItem("CONTEXT/AudioClip/Convert to ogg...", true)]
        public static bool ConvertToOggVorbisValidate(MenuCommand menuCommand)
        {
            if (menuCommand.context is AudioClip audioClip)
            {
                var oldClipPath = AssetDatabase.GetAssetPath(audioClip);
                return !oldClipPath.Contains(".ogg");
            }

            return false;
        }

        [MenuItem("CONTEXT/AudioClip/Convert to ogg...", false)]
        public static void ConvertToOggVorbis(MenuCommand menuCommand)
        {
            var audioClip = menuCommand.context as AudioClip;
            var oldClipPath = AssetDatabase.GetAssetPath(audioClip);
            var newClipPath = oldClipPath.Replace(Path.GetExtension(oldClipPath), ".ogg");

            if (File.Exists(newClipPath) &&
                !EditorUtility.DisplayDialog("Attention!", "Do you want to overwrite the exiting file?", "Yes", "No"))
            {
                return;
            }

            EditorUtility.DisplayProgressBar("Converting clip", $"{oldClipPath} -> {newClipPath}", -1);

            try
            {
                File.WriteAllBytes(newClipPath, audioClip.EncodeToOggVorbis());
                AssetDatabase.ImportAsset(newClipPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
