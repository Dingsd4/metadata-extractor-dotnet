#region License
//
// Copyright 2002-2019 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/metadata-extractor-dotnet
//    https://drewnoakes.com/code/exif/
//
#endregion

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MetadataExtractor.Formats.Exif.Makernotes
{
    /// <summary>Describes tags specific to Apple cameras.</summary>
    /// <remarks>Using information from http://owl.phy.queensu.ca/~phil/exiftool/TagNames/Apple.html</remarks>
    /// <author>Drew Noakes https://drewnoakes.com</author>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class AppleMakernoteDirectory : Directory
    {
        public const int TagRunTime      = 0x0003;
        public const int TagHdrImageType = 0x000a;
        public const int TagBurstUuid    = 0x000b;

        private static readonly Dictionary<int, string> _tagNameMap = new Dictionary<int, string>
        {
            { TagRunTime, "Run Time" },
            { TagHdrImageType, "HDR Image Type" },
            { TagBurstUuid, "Burst UUID" }
        };

        public AppleMakernoteDirectory()
        {
            SetDescriptor(new AppleMakernoteDescriptor(this));
        }

        public override string Name => "Apple Makernote";

        protected override bool TryGetTagName(int tagType, out string tagName)
        {
            return _tagNameMap.TryGetValue(tagType, out tagName);
        }
    }
}