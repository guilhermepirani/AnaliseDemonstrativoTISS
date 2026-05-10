using System.Globalization;
using System.IO;
using System.Text;
using AnaliseDemonstrativoTISS.Operadoras;

namespace AnaliseDemonstrativoTISS.Operadoras.Amil;

public sealed class AnaliseCsv
{
    private const string ColunaFatura = "FATURA";
    private const string ColunaLote = "LOTE";
    private const string ColunaCredencial = "NRO BENEFICIÁRIO";
    private const string ColunaNome = "BENEFICIÁRIO";
    private const string ColunaNumeroGuiaPrestador = "NRO GUIA PRESTADOR";
    private const string ColunaNumeroGuiaOperadora = "NRO GUIA OPERADORA";
    private const string ColunaDataAtendimento = "DATA REALIZAÇÃO";
    private const string ColunaCodigoProcedimento = "CODIGO SERVIÇO";
    private const string ColunaDescricaoProcedimento = "DESCRIÇÃO";
    private const string ColunaQuantidade = "QTD EXECUTADA";
    private const string ColunaValorInformado = "VALOR INFORMADO";
    private const string ColunaValorLiberado = "VALOR LIBERADO";
    private const string ColunaValorGlosa = "VALOR DA GLOSA";
    private const string ColunaCodigoGlosa = "CODIGO DA GLOSA";
    private const string ColunaDescricaoGlosa = "DESCR. DA GLOSA";
    private const string ColunaCodigoGlosaAmil = "CODIGO DA GLOSA AMIL";
    private const string ColunaDescricaoGlosaAmil = "DESCRIÇÃO DA GLOSA AMIL";

    private static readonly HashSet<string> ColunasIgnoradasProtocolo = new(StringComparer.OrdinalIgnoreCase)
    {
        NormalizarCabecalho("FATURA"),
        NormalizarCabecalho("LOTE"),
        NormalizarCabecalho("DATA ENVIO LOTE"),
        NormalizarCabecalho("NRO PROTOCOLO"),
        NormalizarCabecalho("VALOR PROTOCOLO"),
        NormalizarCabecalho("VALOR GLOSA PROTOCOLO"),
        NormalizarCabecalho("COD GLOSA PROTOCOLO")
    };

    private static readonly string[] AliasCredencial = [ColunaCredencial, "NRO BENEFICIARIO", "NUMERO BENEFICIARIO", "NR BENEFICIARIO", "CREDENCIAL"];
    private static readonly string[] AliasNome = [ColunaNome, "BENEFICIARIO", "NOME BENEFICIARIO", "NOME DO BENEFICIARIO", "NOME PACIENTE"];
    private static readonly string[] AliasFatura = [ColunaFatura];
    private static readonly string[] AliasLote = [ColunaLote];
    private static readonly string[] AliasNumeroGuiaPrestador = [ColunaNumeroGuiaPrestador, "NUMERO GUIA PRESTADOR", "NR GUIA PRESTADOR"];
    private static readonly string[] AliasNumeroGuiaOperadora = [ColunaNumeroGuiaOperadora, "NUMERO GUIA OPERADORA", "NR GUIA OPERADORA"];
    private static readonly string[] AliasDataAtendimento = [ColunaDataAtendimento, "DATA REALIZACAO", "DATA ATENDIMENTO", "DT REALIZACAO"];
    private static readonly string[] AliasCodigoProcedimento = [ColunaCodigoProcedimento, "CODIGO SERVICO", "COD SERVICO", "CODIGO PROCEDIMENTO"];
    private static readonly string[] AliasDescricaoProcedimento = [ColunaDescricaoProcedimento, "DESCRICAO", "DESCRICAO SERVICO", "DESCRICAO PROCEDIMENTO"];
    private static readonly string[] AliasQuantidade = [ColunaQuantidade, "QUANTIDADE EXECUTADA", "QTD", "QTDE EXECUTADA"];
    private static readonly string[] AliasValorInformado = [ColunaValorInformado, "VLR INFORMADO", "VALOR COBRADO"];
    private static readonly string[] AliasValorLiberado = [ColunaValorLiberado, "VLR LIBERADO", "VALOR PAGO"];
    private static readonly string[] AliasValorGlosa = [ColunaValorGlosa, "VLR GLOSA", "VALOR GLOSA"];
    private static readonly string[] AliasCodigoGlosa = [ColunaCodigoGlosa, "COD DA GLOSA", "CODIGO GLOSA"];
    private static readonly string[] AliasDescricaoGlosa = [ColunaDescricaoGlosa, "DESCRICAO DA GLOSA", "DESCRICAO GLOSA"];
    private static readonly string[] AliasCodigoGlosaAmil = [ColunaCodigoGlosaAmil, "COD GLOSA AMIL"];
    private static readonly string[] AliasDescricaoGlosaAmil = [ColunaDescricaoGlosaAmil, "DESCRICAO DA GLOSA AMIL", "DESCRICAO GLOSA AMIL"];

    public IReadOnlyList<RegistroAnaliseAmil> Analisar(
        string caminhoCsv,
        CampoFiltroAmil campoFiltro,
        IEnumerable<string> valoresFiltro)
    {
        if (string.IsNullOrWhiteSpace(caminhoCsv))
        {
            throw new ArgumentException("O caminho do CSV deve ser informado.", nameof(caminhoCsv));
        }

        if (!File.Exists(caminhoCsv))
        {
            throw new FileNotFoundException("Arquivo CSV não encontrado.", caminhoCsv);
        }

        var filtros = UtilitariosDeAnalise.CriarFiltros(valoresFiltro);

        var linhas = LerLinhasCsv(caminhoCsv);
        var linhaCabecalho = -1;
        Dictionary<string, int>? indiceColunas = null;

        for (var indiceLinha = 0; indiceLinha < linhas.Length; indiceLinha++)
        {
            if (string.IsNullOrWhiteSpace(linhas[indiceLinha]))
            {
                continue;
            }

            var colunas = SepararColunas(linhas[indiceLinha]);
            var indiceAtual = CriarIndiceColunas(colunas, ignorarColunasProtocolo: false);
            if (!EhCabecalhoAmil(indiceAtual))
            {
                continue;
            }

            linhaCabecalho = indiceLinha;
            indiceColunas = CriarIndiceColunas(colunas, ignorarColunasProtocolo: true);
            break;
        }

        if (linhaCabecalho < 0 || indiceColunas is null)
        {
            throw new InvalidDataException("Não foi possível localizar o cabeçalho do CSV da Amil.");
        }

        var resultado = new List<RegistroAnaliseAmil>();

        for (var indiceLinha = linhaCabecalho + 1; indiceLinha < linhas.Length; indiceLinha++)
        {
            var linha = linhas[indiceLinha];
            if (string.IsNullOrWhiteSpace(linha))
            {
                continue;
            }

            var colunas = SepararColunas(linha);
            if (colunas.All(static coluna => string.IsNullOrWhiteSpace(coluna)))
            {
                continue;
            }

            var registro = CriarRegistro(colunas, indiceColunas);
            if (!UtilitariosDeAnalise.PassaFiltro(
                    filtros,
                    ObterValorCampoFiltro(
                        campoFiltro,
                        registro.Credencial,
                        registro.Nome,
                        registro.NumeroGuiaPrestador,
                        registro.CodigoProcedimento,
                        registro.CodigoGlosa)))
            {
                continue;
            }

            resultado.Add(registro);
        }

        return resultado;
    }

    private static bool EhCabecalhoAmil(Dictionary<string, int> indiceColunas)
    {
        var possuiInicioAmil = PossuiAlgumAlias(indiceColunas, AliasFatura)
                               && PossuiAlgumAlias(indiceColunas, AliasLote);

        var possuiNucleoObrigatorio = PossuiAlgumAlias(indiceColunas, AliasCredencial)
                                      && PossuiAlgumAlias(indiceColunas, AliasNome)
                                      && PossuiAlgumAlias(indiceColunas, AliasNumeroGuiaPrestador);

        return possuiInicioAmil || possuiNucleoObrigatorio;
    }

    private static Dictionary<string, int> CriarIndiceColunas(IReadOnlyList<string> colunas, bool ignorarColunasProtocolo)
    {
        var resultado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var indice = 0; indice < colunas.Count; indice++)
        {
            var nome = NormalizarCabecalho(colunas[indice]);
            if (string.IsNullOrWhiteSpace(nome))
            {
                continue;
            }

            if (ignorarColunasProtocolo && ColunasIgnoradasProtocolo.Contains(nome))
            {
                continue;
            }

            resultado[nome] = indice;
        }

        return resultado;
    }

    private static RegistroAnaliseAmil CriarRegistro(IReadOnlyList<string> colunas, Dictionary<string, int> indiceColunas)
    {
        return new RegistroAnaliseAmil
        {
            Credencial = ObterValorColuna(colunas, indiceColunas, AliasCredencial),
            Nome = ObterValorColuna(colunas, indiceColunas, AliasNome),
            NumeroGuiaPrestador = ObterValorColuna(colunas, indiceColunas, AliasNumeroGuiaPrestador),
            NumeroGuiaOperadora = ObterValorColuna(colunas, indiceColunas, AliasNumeroGuiaOperadora),
            DataAtendimento = UtilitariosDeAnalise.FormatarDataPadrao(ObterValorColuna(colunas, indiceColunas, AliasDataAtendimento)),
            CodigoProcedimento = LimparCodigoProcedimento(ObterValorColuna(colunas, indiceColunas, AliasCodigoProcedimento)),
            DescricaoProcedimento = ObterValorColuna(colunas, indiceColunas, AliasDescricaoProcedimento),
            Quantidade = ObterValorColuna(colunas, indiceColunas, AliasQuantidade),
            ValorInformado = UtilitariosDeAnalise.FormatarValorMonetario(ObterValorColuna(colunas, indiceColunas, AliasValorInformado)),
            ValorLiberado = UtilitariosDeAnalise.FormatarValorMonetario(ObterValorColuna(colunas, indiceColunas, AliasValorLiberado)),
            ValorGlosa = UtilitariosDeAnalise.FormatarValorMonetario(ObterValorColuna(colunas, indiceColunas, AliasValorGlosa)),
            CodigoGlosa = ObterValorColuna(colunas, indiceColunas, AliasCodigoGlosa),
            DescricaoGlosa = ObterValorColuna(colunas, indiceColunas, AliasDescricaoGlosa),
            CodigoGlosaAmil = ObterValorColuna(colunas, indiceColunas, AliasCodigoGlosaAmil),
            DescricaoGlosaAmil = ObterValorColuna(colunas, indiceColunas, AliasDescricaoGlosaAmil)
        };
    }

    private static string ObterValorColuna(
        IReadOnlyList<string> colunas,
        Dictionary<string, int> indiceColunas,
        params string[] nomesColuna)
    {
        var indice = ObterIndiceColuna(indiceColunas, nomesColuna);
        if (indice is null || indice < 0 || indice >= colunas.Count)
        {
            return string.Empty;
        }

        return colunas[indice.Value].Trim();
    }

    private static int? ObterIndiceColuna(Dictionary<string, int> indiceColunas, params string[] nomesColuna)
    {
        foreach (var nomeColuna in nomesColuna)
        {
            var chave = NormalizarCabecalho(nomeColuna);
            if (indiceColunas.TryGetValue(chave, out var indice))
            {
                return indice;
            }
        }

        return null;
    }

    private static bool PossuiAlgumAlias(Dictionary<string, int> indiceColunas, params string[] aliases)
    {
        return ObterIndiceColuna(indiceColunas, aliases) is not null;
    }

    private static IReadOnlyList<string> SepararColunas(string linha)
    {
        var colunas = new List<string>();
        var valorAtual = new StringBuilder();
        var dentroDeAspas = false;

        for (var indice = 0; indice < linha.Length; indice++)
        {
            var caractere = linha[indice];

            if (caractere == '"')
            {
                if (dentroDeAspas && indice + 1 < linha.Length && linha[indice + 1] == '"')
                {
                    valorAtual.Append('"');
                    indice++;
                    continue;
                }

                dentroDeAspas = !dentroDeAspas;
                continue;
            }

            if (caractere == ';' && !dentroDeAspas)
            {
                colunas.Add(valorAtual.ToString().Trim());
                valorAtual.Clear();
                continue;
            }

            valorAtual.Append(caractere);
        }

        colunas.Add(valorAtual.ToString().Trim());
        return colunas;
    }

    private static string ObterValorCampoFiltro(
        CampoFiltroAmil campoFiltro,
        string credencial,
        string nome,
        string numeroGuiaPrestador,
        string codigoProcedimento,
        string codigoGlosa)
    {
        return campoFiltro switch
        {
            CampoFiltroAmil.Credencial => credencial,
            CampoFiltroAmil.Nome => nome,
            CampoFiltroAmil.NumeroGuiaPrestador => numeroGuiaPrestador,
            CampoFiltroAmil.CodigoProcedimento => codigoProcedimento,
            CampoFiltroAmil.CodigoGlosa => codigoGlosa,
            _ => string.Empty
        };
    }

    private static string NormalizarCabecalho(string cabecalho)
    {
        var semAcento = cabecalho.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var caractere in semAcento)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caractere);
            if (categoria == UnicodeCategory.NonSpacingMark ||
                categoria == UnicodeCategory.Format ||
                categoria == UnicodeCategory.Control)
            {
                continue;
            }

            if (char.IsWhiteSpace(caractere))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(caractere));
        }

        return builder.ToString().Trim();
    }

    private static string LimparCodigoProcedimento(string valor)
    {
        var resultado = valor.Trim();
        if (resultado.StartsWith("=", StringComparison.Ordinal))
        {
            resultado = resultado[1..].Trim();
        }

        if (resultado.Length >= 2 && resultado[0] == '"' && resultado[^1] == '"')
        {
            resultado = resultado[1..^1];
        }

        return resultado;
    }

    private static string[] LerLinhasCsv(string caminhoCsv)
    {
        try
        {
            return File.ReadAllLines(caminhoCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException)
        {
            return File.ReadAllLines(caminhoCsv, Encoding.Latin1);
        }
    }
}
