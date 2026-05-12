using ExifEditor.Models;
using ExifEditor.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class PdfGeneratorService {
    private readonly DirectoryService _directoryService;

    private readonly ServiceFactory _serviceFactory;
    public PdfGeneratorService(DirectoryService directoryService, ServiceFactory serviceFactory) {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = true;
        _directoryService = directoryService;
        _serviceFactory = serviceFactory;
    }

    public async Task<string?> GenerateReportAsync(string? dirPath, IProgress<(int current, int total)>? progress = null)
    {
        var imagePaths = _directoryService.GetImagePaths(dirPath);
        var total = imagePaths.Count;
        progress?.Report((0, total));
        var loc = LocalizationService.Current;

        string? outputPath = null;
        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .PaddingTop(100)
                        .AlignCenter()
                        .Text(loc.PdfReport)
                        .Bold().FontSize(36).FontColor(Colors.Black);

                    page.Content()
                        .AlignBottom()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(10);
                            var dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            x.Item().Text($"{loc.PdfGeneratedAt} {dateTime}");
                            x.Item().Text(loc.PdfGeneratedBy);
                        });
                });
                var lastReportedPage = 0;
                for (int i = 0; i < imagePaths.Count; i++) {
                    var imagePath = imagePaths[i];
                    var pageNum = i + 1;
                    var imageService = _serviceFactory.CreateImageService(imagePath);
                    var imageName = Path.GetFileName(imagePath);
                    var descriptionData = DescriptionData.Deserialize(imageService.GetDescription());
                    container.Page(page =>
                    {
                        if (pageNum > lastReportedPage)
                        {
                            lastReportedPage = pageNum;
                            progress?.Report((pageNum, total));
                        }

                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12));

                        page.Header()
                            .PaddingBottom(2)
                            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                            .Text($"{loc.PdfImage}: {imageName}")
                            .Bold().FontSize(12).FontColor(Colors.Black);

                        // A4 content area: 170mm wide x ~257mm tall (after 2cm margins + header/footer)
                        // Image zone: 2/3 of content height, description zone: 1/3
                        const float pageContentWidth = 170f; // mm
                        const float imageZoneHeight = 170f;  // ~2/3 of content area

                        // Calculate image display size preserving aspect ratio
                        using var bitmap = SKBitmap.Decode(imagePath);
                        var imgW = bitmap?.Width ?? 1;
                        var imgH = bitmap?.Height ?? 1;
                        var scale = Math.Min(pageContentWidth / imgW, imageZoneHeight / imgH);
                        var displayWidth = imgW * scale;
                        var displayHeight = imgH * scale;

                        page.Content()
                            .Column(x =>
                            {
                                x.Spacing(8);

                                // Image section — shrinks to actual image size (max 2/3 of page).
                                x.Item().AlignCenter()
                                    .Width(displayWidth, Unit.Millimetre)
                                    .Height(displayHeight, Unit.Millimetre)
                                    .Layers(layers =>
                                    {
                                        layers.PrimaryLayer()
                                            .Image(imagePath)
                                            .WithCompressionQuality(ImageCompressionQuality.Low)
                                            .FitArea();

                                        if (descriptionData.Tags is { Count: > 0 })
                                        {
                                            var numberMap = descriptionData.Tags
                                                .OrderBy(t => t.AnchorY).ThenBy(t => t.AnchorX)
                                                .Select((t, i) => (t, i))
                                                .ToDictionary(x => x.t, x => x.i + 1);
                                            layers.Layer()
                                                .Canvas((canvas, size) =>
                                                {
                                                    DrawTagMarkers(canvas, size, descriptionData.Tags, numberMap);
                                                });
                                        }
                                    });

                                if (descriptionData.Tags is { Count: > 0 })
                                {
                                    var legend = string.Join("; ", descriptionData.Tags
                                        .OrderBy(t => t.AnchorY).ThenBy(t => t.AnchorX)
                                        .Select((t, i) => $"{i + 1} — {t.Label}"));
                                    Section(x, loc.PdfLegend, text => text.Span(legend).Italic());
                                }

                                if (!string.IsNullOrWhiteSpace(descriptionData.Title))
                                {
                                    Section(x, loc.PdfTitle, text => text.Span(descriptionData.Title));
                                }

                                if (!string.IsNullOrWhiteSpace(descriptionData.Scanned))
                                {
                                    Section(x, loc.PdfScanned, text => text.Span(descriptionData.Scanned));
                                }

                                if (!string.IsNullOrWhiteSpace(descriptionData.Description))
                                {
                                    Section(x, loc.PdfDescription, text => text.Span(descriptionData.Description));
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x => x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1));
                    });
                }
            })
            .GeneratePdf(outputPath = Path.Combine(dirPath!, $"report_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf"));
        });
        return outputPath;
    }

    private static void Section(ColumnDescriptor col, string heading, Action<TextDescriptor> body)
    {
        col.Item()
            .PaddingTop(6).PaddingBottom(1)
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .Text(heading).Bold().FontSize(11).FontColor(Colors.Black);
        col.Item().Text(body);
    }

    private static void DrawTagMarkers(SKCanvas canvas, Size size, List<ImageTag> tags, Dictionary<ImageTag, int>? numberMap = null)
    {
        var bubbleRadius = 5.5f;

        using var markerPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 130), // semi-transparent dark
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var textHaloPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 200),
            IsAntialias = true,
            TextSize = 7f,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f
        };

        using var labelTextPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = 7f,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };

        foreach (var tag in tags)
        {
            var cx = (float)(tag.AnchorX * size.Width);
            var cy = (float)(tag.AnchorY * size.Height);

            var displayText = numberMap != null && numberMap.TryGetValue(tag, out var n)
                ? n.ToString()
                : tag.Label;

            if (!string.IsNullOrEmpty(displayText))
            {
                canvas.DrawCircle(cx, cy, bubbleRadius, markerPaint);

                var metrics = labelTextPaint.FontMetrics;
                var textY = cy - (metrics.Ascent + metrics.Descent) / 2;
                canvas.DrawText(displayText, cx, textY, textHaloPaint);
                canvas.DrawText(displayText, cx, textY, labelTextPaint);
            }
        }
    }
}