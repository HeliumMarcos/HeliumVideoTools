using HeliumVideoTools.Core;

namespace HeliumVideoTools.Desktop;

// Pure presentation mapping: no controls, files, processes or mutable job state.
internal sealed record BatchResultPresentation(string Heading,string Counts,string ColorResource,string Details,string Status,bool CompleteProgress)
{
    internal static BatchResultPresentation From(BatchResult result,string? reportWarning)
    {
        static string Count(int n,string one,string many)=>n+" "+(n==1?one:many);
        var counts=new List<string>{Count(result.Succeeded,"concluído","concluídos")};
        if(result.Failed>0)counts.Add($"{result.Failed} com erro");
        int cancelled=result.Items.Count(i=>i.State==BatchItemState.Cancelled);
        if(cancelled>0)counts.Add(Count(cancelled,"cancelado","cancelados"));
        if(result.NotStarted>0)counts.Add(Count(result.NotStarted,"não iniciado","não iniciados"));
        return new(result.Cancelled?"Processamento cancelado":"Resultado do lote",string.Join(" • ",counts),
            result.Cancelled?"TextSecondary":result.Failed>0?result.Succeeded>0?"Warning":"Error":"Success",
            reportWarning??(result.Cancelled?"As saídas já concluídas continuam disponíveis. Os demais arquivos não foram iniciados.":result.Failed>0?"As saídas concluídas foram preservadas. Veja o relatório para identificar os arquivos com erro.":"Processamento concluído. Os arquivos originais foram preservados."),
            reportWarning??(result.Cancelled?"Lote cancelado • resultados concluídos preservados":result.Failed>0?"Lote encerrado com erros • consulte o relatório":"Processamento concluído"),!result.Cancelled);
    }
}
