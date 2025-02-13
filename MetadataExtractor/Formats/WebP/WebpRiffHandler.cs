// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Riff;
using MetadataExtractor.Formats.Xmp;

namespace MetadataExtractor.Formats.WebP
{
    /// <summary>
    /// Implementation of <see cref="IRiffHandler"/> specialising in WebP support.
    /// </summary>
    /// <remarks>
    /// Extracts data from chunk types:
    /// <list type="bullet">
    ///   <item><c>"VP8X"</c>: width, height, is animation, has alpha</item>
    ///   <item><c>"VP8L"</c>: width, height</item>
    ///   <item><c>"VP8 "</c>: width, height</item>
    ///   <item><c>"EXIF"</c>: full Exif data</item>
    ///   <item><c>"ICCP"</c>: full ICC profile</item>
    ///   <item><c>"XMP "</c>: full XMP data</item>
    /// </list>
    /// </remarks>
    public sealed class WebPRiffHandler : IRiffHandler
    {
        private readonly List<Directory> _directories;

        public WebPRiffHandler(List<Directory> directories)
        {
            _directories = directories;
        }

        public bool ShouldAcceptRiffIdentifier(string identifier) => identifier == "WEBP";

        public bool ShouldAcceptChunk(string fourCc) => fourCc == "VP8X" ||
                                                        fourCc == "VP8L" ||
                                                        fourCc == "VP8 " ||
                                                        fourCc == "EXIF" ||
                                                        fourCc == "ICCP" ||
                                                        fourCc == "XMP ";

        public bool ShouldAcceptList(string fourCc) => false;

        public void ProcessChunk(string fourCc, byte[] payload)
        {
            switch (fourCc)
            {
                case "EXIF":
                {
                    // We have seen WebP images with and without the preamble here. It's likely that some software incorrectly
                    // copied an entire JPEG segment into the WebP image. Regardless, we can handle it here.
                    var reader = ExifReader.StartsWithJpegExifPreamble(payload)
                        ? new ByteArrayReader(payload, ExifReader.JpegSegmentPreambleLength)
                        : new ByteArrayReader(payload);
                    _directories.AddRange(new ExifReader().Extract(reader, exifStartOffset: 0));
                    break;
                }
                case "ICCP":
                {
                    _directories.Add(new IccReader().Extract(new ByteArrayReader(payload)));
                    break;
                }
                case "XMP ":
                {
                    _directories.Add(new XmpReader().Extract(payload));
                    break;
                }
                case "VP8X":
                {
                    if (payload.Length != 10)
                        break;

                    string? error = null;
                    var reader = new ByteArrayReader(payload, isMotorolaByteOrder: false);
                    var isAnimation = false;
                    var hasAlpha = false;
                    var widthMinusOne = -1;
                    var heightMinusOne = -1;
                    try
                    {
                        // Flags
                        // bit 0: has fragments
                        // bit 1: is animation
                        // bit 2: has XMP
                        // bit 3: has Exif
                        // bit 4: has alpha
                        // big 5: has ICC
                        isAnimation = reader.GetBit(1);
                        hasAlpha = reader.GetBit(4);

                        // Image size
                        widthMinusOne = reader.GetInt24(4);
                        heightMinusOne = reader.GetInt24(7);
                    }
                    catch (IOException e)
                    {
                        error = "Exception reading WebpRiff chunk 'VP8X' : " + e.Message;
                    }

                    var directory = new WebPDirectory();
                    if (error is null)
                    {
                        directory.Set(WebPDirectory.TagImageWidth, widthMinusOne + 1);
                        directory.Set(WebPDirectory.TagImageHeight, heightMinusOne + 1);
                        directory.Set(WebPDirectory.TagHasAlpha, hasAlpha);
                        directory.Set(WebPDirectory.TagIsAnimation, isAnimation);
                    }
                    else
                        directory.AddError(error);
                    _directories.Add(directory);
                    break;
                }
                case "VP8L":
                {
                    if (payload.Length < 5)
                        break;

                    var reader = new ByteArrayReader(payload, isMotorolaByteOrder: false);

                    string? error = null;
                    var widthMinusOne = -1;
                    var heightMinusOne = -1;
                    try
                    {
                        // https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification#2_riff_header

                        // Expect the signature byte
                        if (reader.GetByte(0) != 0x2F)
                            break;
                        var b1 = reader.GetByte(1);
                        var b2 = reader.GetByte(2);
                        var b3 = reader.GetByte(3);
                        var b4 = reader.GetByte(4);
                        // 14 bits for width
                        widthMinusOne = (b2 & 0x3F) << 8 | b1;
                        // 14 bits for height
                        heightMinusOne = (b4 & 0x0F) << 10 | b3 << 2 | (b2 & 0xC0) >> 6;
                    }
                    catch (IOException e)
                    {
                        error = "Exception reading WebpRiff chunk 'VP8L' : " + e.Message;
                    }

                    var directory = new WebPDirectory();
                    if (error is null)
                    {
                        directory.Set(WebPDirectory.TagImageWidth, widthMinusOne + 1);
                        directory.Set(WebPDirectory.TagImageHeight, heightMinusOne + 1);
                    }
                    else
                        directory.AddError(error);
                    _directories.Add(directory);
                    break;
                }
                case "VP8 ":
                {
                    if (payload.Length < 10)
                        break;

                    var reader = new ByteArrayReader(payload, isMotorolaByteOrder: false);

                    string? error = null;
                    var width = 0;
                    var height = 0;
                    try
                    {
                        // https://tools.ietf.org/html/rfc6386#section-9.1
                        // https://github.com/webmproject/libwebp/blob/master/src/enc/syntax.c#L115

                        // Expect the signature bytes
                        if (reader.GetByte(3) != 0x9D ||
                            reader.GetByte(4) != 0x01 ||
                            reader.GetByte(5) != 0x2A)
                            break;
                        width = reader.GetUInt16(6);
                        height = reader.GetUInt16(8);
                    }
                    catch (IOException e)
                    {
                        error = "Exception reading WebpRiff chunk 'VP8' : " + e.Message;
                    }

                    var directory = new WebPDirectory();
                    if (error is null)
                    {
                        directory.Set(WebPDirectory.TagImageWidth, width);
                        directory.Set(WebPDirectory.TagImageHeight, height);
                    }
                    else
                        directory.AddError(error);
                    _directories.Add(directory);
                    break;
                }
            }
        }

        public void AddError(string errorMessage)
        {
            _directories.Add(new ErrorDirectory(errorMessage));
        }
    }
}
