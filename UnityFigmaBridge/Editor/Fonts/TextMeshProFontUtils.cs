using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Fonts
{
    // Character set code adapted from TMPRo_FontAssetCreatorWindow
    // //https://forum.unity.com/threads/tmp-1-4-1-creating-static-font-assets-via-editor-script.719471/

    public static class TextMeshProFontUtils
    {
        public static void AddBasicCharacterSetToFont(TMP_FontAsset tmpFontAsset)
        {
            var characterSequence = "32 - 126, 160, 8203, 8230, 9633";
            var characterSet = ParseNumberSequence(characterSequence);
            tmpFontAsset.TryAddCharacters(characterSet, out var cantAddChars);
        }

        /// <summary>
        /// Method which returns the character corresponding to a decimal value.
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        static uint[] ParseNumberSequence(string sequence)
        {
            List<uint> unicodeList = new List<uint>();
            string[] sequences = sequence.Split(',');

            foreach (string seq in sequences)
            {
                string[] s1 = seq.Split('-');

                if (s1.Length == 1)
                    try
                    {
                        unicodeList.Add(uint.Parse(s1[0]));
                    }
                    catch
                    {
                        Debug.Log("No characters selected or invalid format.");
                    }
                else
                {
                    for (uint j = uint.Parse(s1[0]); j < uint.Parse(s1[1]) + 1; j++)
                    {
                        unicodeList.Add(j);
                    }
                }
            }

            return unicodeList.ToArray();
        }


        private static uint[] GetCharacterSet(string charSequence)
        {
            // Get list of characters that need to be packed and rendered to the atlas texture.

            var char_List = new List<uint>();

            for (int i = 0; i < charSequence.Length; i++)
            {
                uint unicode = charSequence[i];

                // Handle surrogate pairs
                if (i < charSequence.Length - 1 && char.IsHighSurrogate((char)unicode) &&
                    char.IsLowSurrogate(charSequence[i + 1]))
                {
                    unicode = (uint)char.ConvertToUtf32(charSequence[i], charSequence[i + 1]);
                    i += 1;
                }

                // Check to make sure we don't include duplicates
                if (char_List.FindIndex(item => item == unicode) == -1)
                    char_List.Add(unicode);
            }

            return char_List.ToArray();
        }
    }
}