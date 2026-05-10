using AnaliseDemonstrativoTISS.Operadoras;
using AnaliseDemonstrativoTISS.Operadoras.CaixaDeCubatao;

namespace AnaliseDemonstrativoTISS.Operadoras.Cabesp;

public sealed class AnaliseXml
{
    private readonly CaixaDeCubatao.AnaliseXml _analisadorBase = new();

    public IReadOnlyList<RegistroAnaliseCabesp> Analisar(
        string caminhoXml,
        CampoFiltroCabesp campoFiltro,
        IEnumerable<string> valoresFiltro)
    {
        var campoFiltroBase = MapearCampoFiltro(campoFiltro);
        var registros = _analisadorBase.Analisar(caminhoXml, campoFiltroBase, valoresFiltro);

        return registros.Select(MapearRegistro).ToArray();
    }

    private static RegistroAnaliseCabesp MapearRegistro(RegistroAnalise origem)
    {
        return new RegistroAnaliseCabesp
        {
            Arquivo = origem.Arquivo,
            Credencial = origem.Credencial,
            Nome = origem.Nome,
            Senha = origem.Senha,
            NumeroGuiaPrestador = origem.NumeroGuiaPrestador,
            NumeroGuiaOperadora = origem.NumeroGuiaOperadora,
            DataAtendimento = origem.DataAtendimento,
            CodigoProcedimento = origem.CodigoProcedimento,
            DescricaoProcedimento = origem.DescricaoProcedimento,
            Quantidade = origem.Quantidade,
            ValorInformado = origem.ValorInformado,
            ValorProcessado = origem.ValorProcessado,
            ValorLiberado = origem.ValorLiberado,
            ValorGlosa = origem.ValorGlosa,
            CodigoGlosa = origem.CodigoGlosa,
            DescricaoGlosa = origem.DescricaoGlosa,
            SituacaoGuia = origem.SituacaoGuia
        };
    }

    private static CampoFiltroCaixaCubatao MapearCampoFiltro(CampoFiltroCabesp campoFiltro)
    {
        return campoFiltro switch
        {
            CampoFiltroCabesp.Credencial => CampoFiltroCaixaCubatao.Credencial,
            CampoFiltroCabesp.NumeroGuiaOperadora => CampoFiltroCaixaCubatao.NumeroGuiaOperadora,
            CampoFiltroCabesp.Senha => CampoFiltroCaixaCubatao.Senha,
            CampoFiltroCabesp.CodigoProcedimento => CampoFiltroCaixaCubatao.CodigoProcedimento,
            _ => CampoFiltroCaixaCubatao.Credencial
        };
    }
}
