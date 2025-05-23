using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using PdfExtract.Models;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace PdfExtract.Services;

public sealed partial class VisionHandler : IVisionHandler
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<VisionHandler> _logger;

    private long _totalTokenInput;
    private long _totalTokenOutput;
    private long _totalTokenTotal;

    public VisionHandler(IChatClient chatClient, ILogger<VisionHandler> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    private const string SystemVisionPrompt =
        """
        Agis en tant qu'assistant expert dans l'extraction de données depuis une image,
        l'image en pièce jointe est un extrait de plan.
        Assure-toi d'extraire toutes les entités cohérentes de texte et de ne pas le découper au milieu.
        Le résultat de l'extraction devra être un json avec la structure suivante :
        [{"text":"Le texte cohérent que tu auras trouvé","color":"#FFBBAA"}]
        Assure-toi de ne renvoyer que le json et uniquement le json, si tu ne trouves pas de texte renvoie un tableau vide
        """;

    private const string SystemTextPrompt =
        """
        Agis en tant qu’assistant expert en analyse de données textuelles extraites d’image.

        Tu recevras une liste JSON au format suivant :
        [{"text":"text","color":"#FFBBAA"}]

        Objectif :
        Analyser les textes pour :
        1. Identifier et regrouper les textes identiques (doublons).
        2. Catégoriser chaque texte (ex. : numéro de pièce, dimension, annotation technique, commentaire, inconnu).
        3. Détecter les textes ambigus, incohérents ou partiellement illisibles.

        Consignes de sortie :
        Retourne un seul tableau JSON avec cette structure :
        [{"text":"...","color":"#FFBBAA","category":"...","note":"..."}]
        - text : texte original
        - color : couleur d’origine
        - category : catégorie logique (ex : dimension, référence, nom de pièce, commentaire, inconnu)
        - note : remarque utile (ex : "doublon regroupé", "illisible partiellement", etc.)

        Instructions supplémentaires :
        - Regroupe les textes identiques en une seule entrée et ajoute "note": "doublon regroupé" ou équivalent.
        - Si la liste d’entrée est vide, retourne simplement [].
        - La réponse doit être strictement du JSON — pas de texte explicatif autour.
        """;

    public async IAsyncEnumerable<string> GetTextFromVisionAsync(FileInfo file)
    {
        var imageBytes = await File.ReadAllBytesAsync(file.FullName);

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemVisionPrompt),
            new(ChatRole.User, [new DataContent(imageBytes, "image/webp")])
        };
        _logger.LogInformation("Sending vision request");
        var response = await _chatClient.GetResponseAsync(chatMessages, new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Text,
            MaxOutputTokens = 4096
        });

        var matches = ExtractJson().Matches(response.Text);
        _totalTokenInput += response.Usage?.InputTokenCount ?? 0;
        _totalTokenOutput += response.Usage?.OutputTokenCount ?? 0;
        _totalTokenTotal += response.Usage?.TotalTokenCount ?? 0;
        foreach (Match match in matches)
        {
            yield return match.Groups["json"].Value.Trim();
        }
    }

    public async Task<IEnumerable<Response>> GetTextAsync(IEnumerable<string> files)
    {
        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemTextPrompt),
            new(ChatRole.User, files.Select(i => new TextContent(i)).ToArray<AIContent>())
        };
        _logger.LogInformation("Sending text request");
        var response = await _chatClient.GetResponseAsync(chatMessages, new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Text,
            MaxOutputTokens = 4096
        });

        var output = new List<Response>();
        var matches = ExtractJson().Matches(response.Text);
        _totalTokenInput += response.Usage?.InputTokenCount ?? 0;
        _totalTokenOutput += response.Usage?.OutputTokenCount ?? 0;
        _totalTokenTotal += response.Usage?.TotalTokenCount ?? 0;
        _logger.LogInformation(response.Text);
        foreach (Match match in matches)
        {
            var group = match.Groups["json"].Value.Trim();
            if (group.StartsWith('\u005b'))
            {
                var data = JsonSerializer.Deserialize<List<Response>>(group, JsonSerializerOptions.Web);
                if (data is not null)
                    output.AddRange(data);
            }
            else
            {
                var data = JsonSerializer.Deserialize<Response>(group, JsonSerializerOptions.Web);
                if (data is not null)
                    output.Add(data);
            }
        }

        return output;
    }

    public (long totalInputTokens, long totalOutputTokens, long totalTokens) GetTokens()
    {
        return (_totalTokenInput, _totalTokenOutput, _totalTokenTotal);
    }

    public void ResetTokens()
    {
        _totalTokenInput = 0;
        _totalTokenOutput = 0;
        _totalTokenTotal = 0;
    }

    [GeneratedRegex("```json(?<json>(.*?))```", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ExtractJson();
}