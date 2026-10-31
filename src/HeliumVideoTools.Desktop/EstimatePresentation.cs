using System.Globalization;
using System.IO;
using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;

public sealed record EstimatePresentation(string Summary,string Explanation,string Details)
{
    private static readonly CultureInfo Culture=CultureInfo.GetCultureInfo("pt-BR");
    private static string Mb(double value)=>value<1 ? (value*1000).ToString("0.#",Culture)+" KB" : value>=1000 ? (value/1000).ToString("0.##",Culture)+" GB" : value.ToString("0.0",Culture)+" MB";
    private static string Duration(double seconds)
    {
        var time=TimeSpan.FromSeconds(Math.Clamp(seconds,0,TimeSpan.MaxValue.TotalSeconds-1));
        return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
    public static EstimatePresentation From(IReadOnlyList<EstimateEntry> entries,VideoOptions options)
    {
        var valid=entries.Where(e=>e.Estimate is { } value && (value.CapMb.HasValue || value.BudgetMb.HasValue || value.ReferencePartMb.HasValue)).Select(e=>e.Estimate!).ToArray();
        int problems=entries.Count(e=>e.Error is not null || e.Estimate?.Warning is not null);
        string summary,explanation;
        if(valid.Length==0) { summary="Não há estimativa válida para estes parâmetros"; explanation=entries.Select(e=>e.Error??e.Estimate?.Warning).FirstOrDefault(e=>e is not null)??"Verifique o formato e as permissões. Detalhes por arquivo ao passar o mouse."; }
        else if(options.Operation==Operation.Split && options.SplitMode==SplitMode.Size)
        { summary=$"Teto: {Mb(options.SplitValue)} por parte • {entries.Count} vídeo(s)"; explanation="A quantidade de partes depende da recodificação. O tamanho real será conferido."; }
        else if(options.Operation==Operation.Split)
        {
            double min=valid.Min(e=>e.ReferencePartMb!.Value),max=valid.Max(e=>e.ReferencePartMb!.Value);
            summary=$"Referência: ≈ {(Math.Abs(max-min)<.05 ? Mb(min) : Mb(min)+" a "+Mb(max))}/parte • {valid.Sum(e=>e.Parts)} partes no total";
            explanation=entries.Count==1 ? $"Até {Duration(valid[0].PartSeconds!.Value)} por parte. A recodificação pode alterar o tamanho; não há teto em MB neste modo." : "Baseada na taxa média de cada original. A recodificação pode alterar o tamanho; não há teto em MB neste modo.";
        }
        else
        {
            var budgets=valid.Where(e=>e.BudgetMb.HasValue).ToArray();
            summary=budgets.Length==0 ? "Não há orçamento válido para estes parâmetros" : $"Orçamento{(problems>0?" parcial":"")}: ≈ {Mb(budgets.Sum(e=>e.BudgetMb!.Value))} no total • {budgets.Length} vídeo(s)";
            double minimum=budgets.Length==0?0:budgets.Min(e=>e.CapMb!.Value),maximum=budgets.Length==0?0:budgets.Max(e=>e.CapMb!.Value);
            explanation=budgets.Length==0 ? valid.FirstOrDefault(e=>e.Warning is not null)?.Warning ?? "Escolha outro tamanho para calcular o orçamento." : $"Teto aplicado: {(Math.Abs(minimum-maximum)<.05?Mb(minimum):Mb(minimum)+" a "+Mb(maximum))} por vídeo. O tamanho real pode variar e será conferido.";
        }
        if(problems>0) explanation += $" {problems} arquivo(s) requer(em) atenção; veja os detalhes.";
        var lines=new List<string>();
        foreach(var entry in entries.Take(8))
        {
            string detail=entry.Error ?? entry.Estimate?.Warning ?? (entry.Estimate is { BudgetMb: { } budget,CapMb: { } cap } ? $"orçamento ≈ {Mb(budget)}; teto aplicado {Mb(cap)}" : entry.Estimate is { ReferencePartMb: { } reference,Parts: { } count,PartSeconds: { } seconds } ? $"referência ≈ {Mb(reference)}/parte; {count} partes de até {Duration(seconds)}" : $"até {Mb(entry.Estimate?.CapMb ?? 0)} por parte");
            lines.Add(Path.GetFileName(entry.Source)+" • "+detail);
        }
        if(entries.Count>8) lines.Add($"E mais {entries.Count-8} arquivo(s).");
        return new(summary,explanation,string.Join(Environment.NewLine,lines));
    }
}
