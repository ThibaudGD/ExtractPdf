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
        Agis en tant qu'assistant expert dans l’analyse de données textuelles extraites depuis une image.

        Tu recevras une liste JSON au format suivant :
        [{"text":"text","color":"#FFBBAA"}]

        Ton objectif est d’analyser ces textes pour identifier des informations pertinentes ou des motifs récurrents. 

        Réalise une classification ou une catégorisation logique si applicable (ex : numéro de pièce, dimensions, annotations techniques, etc.). 
        Si certains textes semblent ambigus ou incohérents, indique-les.
        Si des textes sont en double, regroupes-les et indique-les.
        Ta réponse devra être un SEUL et UNIQUE tableau JSON avec la structure suivante :
        [{"text":"...","color":"#FFBBAA","category":"...","note":"..."}]

        - "text" : le texte d’origine
        - "color" : la couleur du texte (ex : #FFBBAA)
        - "category" : la catégorie identifiée (ex : dimension, nom de pièce, référence, commentaire, inconnu)
        - "note" : optionnel, un commentaire ou une remarque utile (ex : "possiblement une cote", "illisible partiellement", etc.)

        Si la liste fournie est vide, retourne simplement un tableau vide.
        Assure-toi de ne renvoyer que le JSON, et uniquement le JSON.
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
        _logger.LogInformation(response.Text);

        var output = new List<Response>();
        var matches = ExtractJson().Matches(response.Text);
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
    
    
    [GeneratedRegex("```json(?<json>(.*?))```", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ExtractJson();
}