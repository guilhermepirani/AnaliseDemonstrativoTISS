using System.IO;
using System.Xml.Linq;
using AnaliseDemonstrativoTISS.Operadoras;

namespace AnaliseDemonstrativoTISS.Operadoras.Geap;

public sealed class AnaliseXml
{
    private static readonly string[] TagsCredencial = ["NroCarteira", "NroInscricao"];
    private static readonly string[] TagsDataAtendimento = ["DtaAtendimento", "DtaQuitada"];
    private static readonly string[] TagsQuantidade = ["QtdServico", "Quantidade"];
    private static readonly string[] TagsValorInformado = ["VlrInformado"];
    private static readonly string[] TagsValorProcessado = ["VlrCalculado"];
    private static readonly string[] TagsValorGlosa = ["VlrGlosado"];

    public IReadOnlyList<RegistroAnaliseGeap> Analisar(
        string caminhoXml,
        CampoFiltroGeap campoFiltro,
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
        var resultado = new List<RegistroAnaliseGeap>();

        foreach (var guiaPortal in ObterGuiasPortal(documento))
        {
            var guia = ObterGuia(guiaPortal);
            var dadosGuia = ExtrairDadosGuia(guia);
            var itens = ObterItensGuia(guiaPortal).ToList();

            if (itens.Count == 0)
            {
                var registroBase = CriarRegistroBase(dadosGuia);
                if (PassaFiltro(filtros, campoFiltro, registroBase))
                {
                    resultado.Add(registroBase);
                }

                continue;
            }

            foreach (var item in itens)
            {
                var registro = CriarRegistroComItem(dadosGuia, item);
                if (PassaFiltro(filtros, campoFiltro, registro))
                {
                    resultado.Add(registro);
                }
            }
        }

        return resultado;
    }

    private static DadosGuia ExtrairDadosGuia(XElement? guia)
    {
        if (guia is null)
        {
            return new DadosGuia();
        }

        var valorProcessado = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorProcessado));
        var valorGlosa = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorGlosa));

        return new DadosGuia(
            Credencial: BuscarPrimeiroValor(guia, TagsCredencial),
            Nome: BuscarPrimeiroValor(guia, "NmeCliente"),
            NumeroGuiaPrestador: BuscarPrimeiroValor(guia, "NroGspContratado"),
            NumeroGuiaOperadora: BuscarPrimeiroValor(guia, "NroGsp"),
            DataAtendimento: UtilitariosDeAnalise.FormatarDataPadrao(BuscarPrimeiroValor(guia, "DtaQuitada"), mesPrimeiro: true),
            ValorInformado: UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorInformado)),
            ValorProcessado: valorProcessado,
            ValorLiberado: valorProcessado,
            ValorGlosa: valorGlosa,
            SituacaoGuia: BuscarPrimeiroValor(guia, "ExisteGlosas"));
    }

    private static RegistroAnaliseGeap CriarRegistroBase(DadosGuia dadosGuia)
    {
        return new RegistroAnaliseGeap
        {
            Credencial = dadosGuia.Credencial,
            Nome = dadosGuia.Nome,
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

    private static RegistroAnaliseGeap CriarRegistroComItem(DadosGuia dadosGuia, XElement item)
    {
        var valorProcessadoItem = BuscarPrimeiroValor(item, TagsValorProcessado);
        var valorGlosaItem = BuscarPrimeiroValor(item, TagsValorGlosa);
        var valorProcessadoFormatado = UtilitariosDeAnalise.FormatarValorMonetario(string.IsNullOrWhiteSpace(valorProcessadoItem) ? dadosGuia.ValorProcessado : valorProcessadoItem);

        return new RegistroAnaliseGeap
        {
            Credencial = BuscarPrimeiroValor(item, TagsCredencial),
            Nome = dadosGuia.Nome,
            NumeroGuiaPrestador = dadosGuia.NumeroGuiaPrestador,
            NumeroGuiaOperadora = BuscarPrimeiroValor(item, "NroGsp"),
            DataAtendimento = UtilitariosDeAnalise.FormatarDataPadrao(BuscarPrimeiroValor(item, TagsDataAtendimento), mesPrimeiro: true),
            CodigoProcedimento = BuscarPrimeiroValor(item, "NroServico"),
            DescricaoProcedimento = BuscarPrimeiroValor(item, "NmeServico"),
            Quantidade = BuscarPrimeiroValor(item, TagsQuantidade),
            ValorInformado = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(item, TagsValorInformado)),
            ValorProcessado = valorProcessadoFormatado,
            ValorLiberado = valorProcessadoFormatado,
            ValorGlosa = UtilitariosDeAnalise.FormatarValorMonetario(string.IsNullOrWhiteSpace(valorGlosaItem) ? dadosGuia.ValorGlosa : valorGlosaItem),
            CodigoGlosa = BuscarPrimeiroValor(item, "NroJustificativa"),
            SituacaoGuia = dadosGuia.SituacaoGuia
        };
    }

    private static IEnumerable<XElement> ObterGuiasPortal(XDocument documento)
    {
        return documento.Descendants().Where(static elemento => NomeEh(elemento, "GuiaPortalXml"));
    }

    private static XElement? ObterGuia(XElement guiaPortal)
    {
        return guiaPortal.Elements().FirstOrDefault(static elemento => NomeEh(elemento, "Guia"));
    }

    private static IEnumerable<XElement> ObterItensGuia(XElement guiaPortal)
    {
        return guiaPortal.Descendants().Where(static elemento => NomeEh(elemento, "ItemGuiaPortal"));
    }

    private static bool PassaFiltro(HashSet<string> filtros, CampoFiltroGeap campoFiltro, RegistroAnaliseGeap registro)
    {
        return UtilitariosDeAnalise.PassaFiltro(filtros, ObterValorCampoFiltro(campoFiltro, registro));
    }

    private static string ObterValorCampoFiltro(CampoFiltroGeap campoFiltro, RegistroAnaliseGeap registro)
    {
        return campoFiltro switch
        {
            CampoFiltroGeap.Credencial => registro.Credencial,
            CampoFiltroGeap.Nome => registro.Nome,
            CampoFiltroGeap.NumeroGuiaPrestador => registro.NumeroGuiaPrestador,
            CampoFiltroGeap.NumeroGuiaOperadora => registro.NumeroGuiaOperadora,
            CampoFiltroGeap.CodigoProcedimento => registro.CodigoProcedimento,
            _ => string.Empty
        };
    }

    private static string BuscarPrimeiroValor(XElement? origem, params string[] nomes)
    {
        if (origem is null)
        {
            return string.Empty;
        }

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
        string Credencial = "",
        string Nome = "",
        string NumeroGuiaPrestador = "",
        string NumeroGuiaOperadora = "",
        string DataAtendimento = "",
        string ValorInformado = "",
        string ValorProcessado = "",
        string ValorLiberado = "",
        string ValorGlosa = "",
        string SituacaoGuia = "");
}
