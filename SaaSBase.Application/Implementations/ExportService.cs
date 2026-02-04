using OfficeOpenXml;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

namespace SaaSBase.Application.Implementations;

public class ExportService<T>
{
	public async Task<byte[]> ExportToCsvAsync(IEnumerable<T> data)
	{
		var csv = new StringBuilder();
		
		// Get properties via reflection
		var properties = typeof(T).GetProperties()
			.Where(p => p.CanRead)
			.ToArray();

		// Add headers
		csv.AppendLine(string.Join(",", properties.Select(p => $"\"{p.Name}\"")));

		// Add data rows
		foreach (var item in data)
		{
			var values = properties.Select(p =>
			{
				var value = p.GetValue(item);
				if (value == null) return "\"\"";
				
				// Handle arrays/collections
				if (value is System.Collections.IEnumerable enumerable && !(value is string))
				{
					var items = enumerable.Cast<object>().Select(i => i.ToString()).ToArray();
					return $"\"{string.Join("; ", items)}\"";
				}
				
				// Handle dates
				if (value is DateTime dateTime)
				{
					return $"\"{dateTime:yyyy-MM-dd HH:mm:ss}\"";
				}
				
				// Handle other values
				var stringValue = value.ToString()?.Replace("\"", "\"\"") ?? "";
				return $"\"{stringValue}\"";
			});
			
			csv.AppendLine(string.Join(",", values));
		}

		return await Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
	}

	public async Task<byte[]> ExportToExcelAsync(IEnumerable<T> data)
	{
		ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
		
		using var package = new ExcelPackage();
		var worksheet = package.Workbook.Worksheets.Add("Roles Export");
		
		// Get properties via reflection
		var properties = typeof(T).GetProperties()
			.Where(p => p.CanRead)
			.ToArray();

		// Add headers
		for (int i = 0; i < properties.Length; i++)
		{
			worksheet.Cells[1, i + 1].Value = properties[i].Name;
			worksheet.Cells[1, i + 1].Style.Font.Bold = true;
		}

		// Add data rows
		var row = 2;
		foreach (var item in data)
		{
			for (int col = 0; col < properties.Length; col++)
			{
				var value = properties[col].GetValue(item);
				
				// Handle arrays/collections
				if (value is System.Collections.IEnumerable enumerable && !(value is string))
				{
					var items = enumerable.Cast<object>().Select(i => i.ToString()).ToArray();
					worksheet.Cells[row, col + 1].Value = string.Join("; ", items);
				}
				else
				{
					worksheet.Cells[row, col + 1].Value = value;
				}
			}
			row++;
		}

		// Auto-fit columns
		worksheet.Cells.AutoFitColumns();

		return await Task.FromResult(package.GetAsByteArray());
	}

    public async Task<byte[]> ExportToPdfAsync(IEnumerable<T> data, string title = "Export")
    {
        // Reflect properties once
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead)
            .ToArray();

        var items = data.ToList();

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text(title)
                        .FontSize(18)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken2);

                    row.ConstantItem(150).AlignRight().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                });

                page.Content().Table(table =>
                {
                    // Columns
                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < properties.Length; i++)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    // Header
                    table.Header(header =>
                    {
                        for (int i = 0; i < properties.Length; i++)
                        {
                            header.Cell().Element(CellHeaderStyle).Text(properties[i].Name);
                        }

                        static IContainer CellHeaderStyle(IContainer container)
                        {
                            return container
                                .PaddingVertical(6)
                                .PaddingHorizontal(4)
                                .Background(Colors.Grey.Lighten3)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .DefaultTextStyle(x => x.SemiBold());
                        }
                    });

                    // Rows
                    foreach (var item in items)
                    {
                        for (int i = 0; i < properties.Length; i++)
                        {
                            var value = properties[i].GetValue(item);
                            string text;

                            if (value == null)
                            {
                                text = string.Empty;
                            }
                            else if (value is System.Collections.IEnumerable enumerable && value is not string)
                            {
                                var arr = enumerable.Cast<object?>().Select(v => v?.ToString()).Where(v => v != null);
                                text = string.Join("; ", arr);
                            }
                            else if (value is DateTime dt)
                            {
                                text = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            else if (value is DateTimeOffset dto)
                            {
                                text = dto.ToString("yyyy-MM-dd HH:mm:ss zzz");
                            }
                            else
                            {
                                text = value.ToString() ?? string.Empty;
                            }

                            table.Cell().Element(CellBodyStyle).Text(text);

                            static IContainer CellBodyStyle(IContainer container)
                            {
                                return container
                                    .PaddingVertical(4)
                                    .PaddingHorizontal(4)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten3);
                            }
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return await Task.FromResult(stream.ToArray());
    }
}
