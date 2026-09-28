using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Renders legal complaint drafts and formal application templates into
/// downloadable single-page PDFs using QuestPDF matching the official legal aid design.
/// </summary>
public sealed class QuestPdfService : IPdfService
{
    private static readonly string[] BanglaFontStack = ["Kalpurush", "kalpurush", "Nikosh", "Lohit Bengali", "Mukti", "Noto Sans Bengali", "Nirmala UI", "Siyam Rupali", "SolaimanLipi", "Vrinda", "Arial", "Segoe UI"];

    // Odhikar Dashboard Theme Color Palette
    private static readonly Color GovPrimary = Color.FromHex("#005A44");
    private static readonly Color GovPrimaryLight = Color.FromHex("#EBF4F0");
    private static readonly Color GovPrimaryBorder = Color.FromHex("#E2E6E3");
    private static readonly Color GovTextDark = Color.FromHex("#1A2421");
    private static readonly Color GovTextMuted = Color.FromHex("#6C7873");

    static QuestPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        try
        {
            var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "kalpurush.ttf");
            if (File.Exists(fontPath))
            {
                using var stream = File.OpenRead(fontPath);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuestPdfService] Font registration note: {ex.Message}");
        }
    }

    public QuestPdfService()
    {
    }

    public byte[] GenerateComplaintPdf(CaseModel @case, string complaintText)
    {
        return GenerateApplicationPdf(new ApplicationPdfDocumentModel
        {
            CaseNumber = @case.CaseNumber,
            CreatedAt = @case.CreatedAt,
            ParentCategoryNameBn = @case.Category,
            ParentCategoryCode = @case.Category,
            TemplateTitleBn = "আইনি প্রতিকার ও সহায়তা আবেদন",
            ApplicationBodyBn = complaintText,
            ApplicantName = @case.ExtractedData.TryGetValue("applicant", out var a) ? (a.Value ?? string.Empty) : string.Empty,
            DisclaimerNoticeBn = "সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।"
        });
    }

    public byte[] GenerateApplicationPdf(ApplicationPdfDocumentModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.4f, Unit.Centimetre);
                page.MarginVertical(1.1f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(BanglaFontStack).FontColor(GovTextDark));

                // --- 1. GOVERNMENT TOP HEADER ---
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Column(c =>
                    {
                        c.Item().Text("গণপ্রজাতন্ত্রী বাংলাদেশ সরকার").Bold().FontSize(13f).FontColor(GovTextDark);
                        c.Item().PaddingTop(1).Text("আইনি প্রতিকার ও সহায়তা আবেদনপত্র").SemiBold().FontSize(9.5f).FontColor(GovPrimary);
                    });

                    col.Item().PaddingTop(3).LineHorizontal(1.5f).LineColor(GovPrimary);
                });

                // --- 2. CONTENT ---
                page.Content().PaddingVertical(4).Column(col =>
                {
                    col.Spacing(4);

                    // Docket Box
                    col.Item().Border(0.8f).BorderColor(GovPrimaryBorder).Background(GovPrimaryLight).Padding(4).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"স্মারক / কেস নম্বর: {model.CaseNumber}").Bold().FontSize(8.5f).FontColor(GovPrimary);
                        });

                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().Text($"মোকদ্দমার শ্রেণি: {model.ParentCategoryNameBn}").Bold().FontSize(8.5f).FontColor(GovPrimary);
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"তারিখ: {FormatBanglaDate(model.CreatedAt)}").FontSize(8.5f).FontColor(GovTextMuted);
                        });
                    });

                    // Title (Centered Bold)
                    var title = string.IsNullOrWhiteSpace(model.TemplateTitleBn)
                        ? "নারী ও শিশু সুরক্ষা ও অধিকার সংরক্ষণ আবেদন"
                        : model.TemplateTitleBn;

                    col.Item().PaddingTop(2).PaddingBottom(2).AlignCenter().Column(c =>
                    {
                        c.Item().Text(title).Bold().FontSize(12f).FontColor(GovPrimary);
                    });

                    // Application Body Text
                    col.Item().PaddingTop(1).Text(model.ApplicationBodyBn).LineHeight(1.26f);

                    // Right-Aligned Applicant Signature Block
                    col.Item().PaddingTop(8).AlignRight().Column(c =>
                    {
                        var appName = string.IsNullOrWhiteSpace(model.ApplicantName) ? "[আবেদনকারীর নাম]" : model.ApplicantName;
                        c.Item().Text($"আবেদনকারী: {appName}").Bold().FontSize(9f);
                        c.Item().PaddingTop(2).Text("স্বাক্ষর / টিপসহি: .......................................").FontSize(8.5f).FontColor(GovTextMuted);
                        c.Item().PaddingTop(1).Text($"তারিখ: {FormatBanglaDate(model.CreatedAt)}").FontSize(8f).FontColor(GovTextMuted);
                    });

                    // Legal Notice Box (#F8FAFC bg, grey border)
                    if (!string.IsNullOrWhiteSpace(model.DisclaimerNoticeBn))
                    {
                        col.Item().PaddingTop(4).Border(0.6f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(4).Column(c =>
                        {
                            c.Item().Text(text =>
                            {
                                text.Span("সরকারি আইনি বিজ্ঞপ্তি: ").Bold().FontSize(7f).FontColor(GovTextMuted);
                                text.Span(model.DisclaimerNoticeBn).FontSize(6.8f).FontColor(GovTextMuted);
                            });
                        });
                    }
                });

                // --- 3. FOOTER ---
                page.Footer().Column(col =>
                {
                    col.Item().PaddingBottom(2).LineHorizontal(1f).LineColor(GovPrimary);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("গণপ্রজাতন্ত্রী বাংলাদেশ সরকার • অধিকার (Odhikar)").FontSize(7.5f).FontColor(GovTextMuted);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(GovTextMuted));
                            text.Span("পৃষ্ঠা ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string FormatBanglaDate(DateTime dt)
    {
        var raw = dt.ToString("dd-MM-yyyy");
        return raw.Replace('0', '০')
                  .Replace('1', '১')
                  .Replace('2', '২')
                  .Replace('3', '৩')
                  .Replace('4', '৪')
                  .Replace('5', '৫')
                  .Replace('6', '⑥') // safe fallback
                  .Replace('6', '৬')
                  .Replace('7', '৭')
                  .Replace('8', '৮')
                  .Replace('9', '৯');
    }
}
