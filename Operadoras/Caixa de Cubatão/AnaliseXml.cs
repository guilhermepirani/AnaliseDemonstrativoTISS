using System.IO;
using System.Xml.Linq;
using AnaliseDemonstrativoTISS.Operadoras;

namespace AnaliseDemonstrativoTISS.Operadoras.CaixaDeCubatao;

public sealed class AnaliseXml
{
    private static readonly string[] TagsCredencial = ["numeroCarteira", "numeroCarteiraBeneficiario", "credencial", "matricula"];
    private static readonly string[] TagsDataGuia = ["dataRealizacao", "dataAtendimento", "dataExecucao", "dataInicioFat", "dataFimFat"];
    private static readonly string[] TagsValorInformadoGuia = ["valorInformadoGuia", "valorInformado"];
    private static readonly string[] TagsValorProcessadoGuia = ["valorProcessadoGuia", "valorProcessado"];
    private static readonly string[] TagsValorLiberadoGuia = ["valorLiberadoGuia", "valorLiberado"];
    private static readonly string[] TagsValorGlosaGuia = ["valorGlosaGuia", "valorGlosa"];
    private static readonly string[] TagsCodigoProcedimento = ["codigoProcedimento", "codigo", "procedimento"];
    private static readonly string[] TagsDescricaoProcedimento = ["descricaoProcedimento", "descricao"];
    private static readonly string[] TagsQuantidadeProcedimento = ["qtdExecutada", "quantidadeExecutada", "quantidade"];
    private static readonly string[] TagsValorInformadoProcedimento = ["valorInformado", "valorTotal", "valorProcedimento"];
    private static readonly string[] TagsValorProcessadoProcedimento = ["valorProcessado"];
    private static readonly string[] TagsValorLiberadoProcedimento = ["valorLiberado"];

    public IReadOnlyList<RegistroAnaliseCxCubatao> Analisar(
        string caminhoXml,
        CampoFiltroCaixaCubatao campoFiltro,
        IEnumerable<string> valoresFiltro)
    {
        if (string.IsNullOrWhiteSpace(caminhoXml))
        {
            throw new ArgumentException("O caminho do XML deve ser informado.", nameof(caminhoXml));
        }

        if (!File.Exists(caminhoXml))
        {
            throw new FileNotFoundException("Arquivo XML não encontrado.", caminhoXml);
        }

        var filtros = UtilitariosDeAnalise.CriarFiltros(valoresFiltro);
        var documento = UtilitariosDeAnalise.CarregarDocumentoXml(caminhoXml);
        var resultado = new List<RegistroAnaliseCxCubatao>();

        foreach (var guia in ObterGuias(documento))
        {
            var dadosGuia = ExtrairDadosGuia(guia);
            var detalhes = ObterDetalhesGuia(guia).ToList();
            if (detalhes.Count == 0)
            {
                var registroBase = CriarRegistroBase(dadosGuia);
                if (PassaFiltro(filtros, campoFiltro, registroBase))
                {
                    resultado.Add(registroBase);
                }

                continue;
            }

            foreach (var detalhe in detalhes)
            {
                var registroComDetalhe = CriarRegistroComDetalhe(dadosGuia, detalhe);
                if (PassaFiltro(filtros, campoFiltro, registroComDetalhe))
                {
                    resultado.Add(registroComDetalhe);
                }
            }
        }

        return resultado;
    }

    private static DadosGuia ExtrairDadosGuia(XElement guia)
    {
        return new DadosGuia(
            Credencial: BuscarPrimeiroValor(guia, TagsCredencial),
            Senha: BuscarPrimeiroValor(guia, "senha"),
            NumeroGuiaPrestador: BuscarPrimeiroValor(guia, "numeroGuiaPrestador"),
            NumeroGuiaOperadora: BuscarPrimeiroValor(guia, "numeroGuiaOperadora"),
            DataAtendimento: UtilitariosDeAnalise.FormatarDataPadrao(BuscarPrimeiroValor(guia, TagsDataGuia)),
            ValorInformado: UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorInformadoGuia)),
            ValorProcessado: UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorProcessadoGuia)),
            ValorLiberado: UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorLiberadoGuia)),
            ValorGlosa: UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorGlosaGuia)),
            SituacaoGuia: BuscarPrimeiroValor(guia, "situacaoGuia"));
    }

    private static RegistroAnaliseCxCubatao CriarRegistroBase(DadosGuia dadosGuia)
    {
        return new RegistroAnaliseCxCubatao
        {
            Credencial = dadosGuia.Credencial,
            Senha = dadosGuia.Senha,
            NumeroGuiaPrestador = dadosGuia.NumeroGuiaPrestador,
            NumeroGuiaOperadora = dadosGuia.NumeroGuiaOperadora,
            DataAtendimento = dadosGuia.DataAtendimento,
            ValorInformado = dadosGuia.ValorInformado,
            ValorProcessado = dadosGuia.ValorProcessado,
            ValorLiberado = dadosGuia.ValorLiberado,
            ValorGlosa = dadosGuia.ValorGlosa,
            SituacaoGuia = dadosGuia.SituacaoGuia
        };
    }

    private static RegistroAnaliseCxCubatao CriarRegistroComDetalhe(DadosGuia dadosGuia, XElement detalhe)
    {
        var valorInformadoDetalhe = BuscarPrimeiroValor(detalhe, TagsValorInformadoProcedimento);
        var valorProcessadoDetalhe = BuscarPrimeiroValor(detalhe, TagsValorProcessadoProcedimento);
        var valorLiberadoDetalhe = BuscarPrimeiroValor(detalhe, TagsValorLiberadoProcedimento);

        return new RegistroAnaliseCxCubatao
        {
            Credencial = dadosGuia.Credencial,
            Senha = dadosGuia.Senha,
            NumeroGuiaPrestador = dadosGuia.NumeroGuiaPrestador,
            NumeroGuiaOperadora = dadosGuia.NumeroGuiaOperadora,
            DataAtendimento = UtilitariosDeAnalise.FormatarDataPadrao(BuscarPrimeiroValor(detalhe, TagsDataGuia)),
            CodigoProcedimento = BuscarPrimeiroValor(detalhe, TagsCodigoProcedimento),
            DescricaoProcedimento = BuscarPrimeiroValor(detalhe, TagsDescricaoProcedimento),
            Quantidade = BuscarPrimeiroValor(detalhe, TagsQuantidadeProcedimento),
            ValorInformado = UtilitariosDeAnalise.FormatarValorMonetario(string.IsNullOrWhiteSpace(valorInformadoDetalhe) ? dadosGuia.ValorInformado : valorInformadoDetalhe),
            ValorProcessado = UtilitariosDeAnalise.FormatarValorMonetario(string.IsNullOrWhiteSpace(valorProcessadoDetalhe) ? dadosGuia.ValorProcessado : valorProcessadoDetalhe),
            ValorLiberado = UtilitariosDeAnalise.FormatarValorMonetario(string.IsNullOrWhiteSpace(valorLiberadoDetalhe) ? dadosGuia.ValorLiberado : valorLiberadoDetalhe),
            ValorGlosa = dadosGuia.ValorGlosa,
            SituacaoGuia = dadosGuia.SituacaoGuia
        };
    }

    private static bool PassaFiltro(HashSet<string> filtros, CampoFiltroCaixaCubatao campoFiltro, RegistroAnaliseCxCubatao registro)
    {
        return UtilitariosDeAnalise.PassaFiltro(filtros, ObterValorCampoFiltro(campoFiltro, registro));
    }

    private static IEnumerable<XElement> ObterGuias(XDocument documento)
    {
        return documento.Descendants().Where(static elemento => NomeEh(elemento, "relacaoGuias"));
    }

    private static IEnumerable<XElement> ObterDetalhesGuia(XElement guia)
    {
        return guia.Descendants().Where(static elemento => NomeEh(elemento, "detalhesGuia"));
    }

    private static string ObterValorCampoFiltro(CampoFiltroCaixaCubatao campoFiltro, RegistroAnaliseCxCubatao registro)
    {
        return campoFiltro switch
        {
            CampoFiltroCaixaCubatao.Credencial => registro.Credencial,
            CampoFiltroCaixaCubatao.NumeroGuiaOperadora => registro.NumeroGuiaOperadora,
            CampoFiltroCaixaCubatao.Senha => registro.Senha,
            CampoFiltroCaixaCubatao.CodigoProcedimento => registro.CodigoProcedimento,
            _ => string.Empty
        };
    }

    private static string BuscarPrimeiroValor(XElement origem, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            var encontrado = origem.DescendantsAndSelf().FirstOrDefault(elemento => NomeEh(elemento, nome));
            if (encontrado is null)
            {
                continue;
            }

            var valor = encontrado.Value.Trim();
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return string.Empty;
    }

    private static bool NomeEh(XElement elemento, string nome)
    {
        return string.Equals(elemento.Name.LocalName, nome, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct DadosGuia(
        string Credencial,
        string Senha,
        string NumeroGuiaPrestador,
        string NumeroGuiaOperadora,
        string DataAtendimento,
        string ValorInformado,
        string ValorProcessado,
        string ValorLiberado,
        string ValorGlosa,
        string SituacaoGuia);
}
