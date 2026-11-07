using System.Globalization;
using EmployeeTime.Web.Models;
using ScottPlot;

namespace EmployeeTime.Web.Services;

public sealed class PieChartRendererService
{
    private static readonly string[] Palette =
    [
        "#1F6D5A", "#DC764C", "#6E82B6", "#D8AC42", "#9A6B91",
        "#4E969D", "#947653", "#99AA64", "#D68491", "#607184"
    ];

    public static string ColorForIndex(int index) => Palette[index % Palette.Length];

    public byte[] Render(TimeReport report, bool includeLegend = true)
    {
        using Plot plot = new();
        plot.Axes.Frameless();
        plot.HideGrid();

        if (report.TotalHours <= 0)
        {
            throw new ArgumentException("A pie chart needs a positive total.", nameof(report));
        }

        PieSlice[] slices = report.Employees.Select((employee, index) => new PieSlice
        {
            Value = (double)employee.TotalHours,
            FillColor = Color.FromHex(ColorForIndex(index)),
            Label = employee.SharePercent >= 3m
                ? employee.SharePercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"
                : string.Empty,
            LabelFontSize = 23,
            LabelFontColor = Colors.White,
            LegendText = $"{employee.Name}  {employee.SharePercent.ToString("0.0", CultureInfo.InvariantCulture)}%"
        }).ToArray();

        var pie = plot.Add.Pie(slices);
        pie.SliceLabelDistance = 0.69;
        pie.LineColor = Colors.White;
        pie.LineWidth = 3;
        if (includeLegend)
        {
            plot.ShowLegend(Edge.Right);
            plot.Legend.FontSize = 21;
            plot.Legend.FontName = "Arial";
            plot.Legend.OutlineColor = Colors.Transparent;
            plot.Legend.ShadowColor = Colors.Transparent;
            plot.Legend.BackgroundColor = Colors.White;
        }
        else
        {
            plot.HideLegend();
        }
        return plot.GetImage(includeLegend ? 1200 : 720, includeLegend ? 760 : 720).GetImageBytes();
    }
}
