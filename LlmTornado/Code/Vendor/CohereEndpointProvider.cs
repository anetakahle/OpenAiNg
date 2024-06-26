using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using LlmTornado.Chat;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;

namespace LlmTornado.Code.Vendor;

/// <summary>
/// 
/// </summary>
internal class CohereEndpointProvider : BaseEndpointProvider, IEndpointProvider, IEndpointProviderExtended
{
    private const string Event = "event:";
    private const string Data = "data:";
    private const string StreamMsgStart = $"{Event} message_start";
    private const string StreamMsgStop = $"{Event} message_stop";
    private const string StreamPing = $"{Event} ping";
    private const string StreamContentBlockDelta = $"{Event} content_block_delta";
    private const string StreamContentBlockStart = $"{Event} content_block_start";
    private const string StreamContentBlockStop = $"{Event} content_block_stop";
    private static readonly HashSet<string> StreamSkip = [StreamMsgStart, StreamMsgStop, StreamPing];
    private static readonly HashSet<string> toolFinishReasons = [ "tool_use" ];
    
    public static Version OutboundVersion { get; set; } = HttpVersion.Version20;
    public override HashSet<string> ToolFinishReasons => toolFinishReasons;
    
    private enum StreamNextAction
    {
        Read,
        BlockStart,
        BlockDelta,
        BlockStop,
        Skip,
        MsgStart
    }
    
    public CohereEndpointProvider(TornadoApi api) : base(api)
    {
        Provider = LLmProviders.Cohere;
        StoreApiAuth();
    }

    private class AnthropicStreamBlockStart
    {
        public class AnthropicStreamBlockStartData
        {
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("text")]
            public string Text { get; set; }
        }
        
        [JsonProperty("index")]
        public int Index { get; set; }
        [JsonProperty("content_block")]
        public AnthropicStreamBlockStartData ContentBlock { get; set; }
    }

    private class AnthropicStreamBlockDelta
    {
        public class AnthropicStreamBlockDeltaData
        {
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("text")]
            public string Text { get; set; }
        }
        
        [JsonProperty("index")]
        public int Index { get; set; }
        [JsonProperty("delta")]
        public AnthropicStreamBlockDeltaData Delta { get; set; }
    }

    private class AnthropicStreamBlockStop
    {
        [JsonProperty("index")]
        public int Index { get; set; }
    }

    private class AnthropicStreamMsgStart
    {
        [JsonProperty("message")]
        public VendorAnthropicChatResult Message { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    public override string ApiUrl(CapabilityEndpoints endpoint, string? url)
    {
        string eStr = endpoint switch
        {
            CapabilityEndpoints.Chat => "chat",
            _ => throw new Exception($"Cohere doesn't support endpoint {endpoint}")
        };

        return $"https://api.cohere.ai/v1/{eStr}{url}";
    }

    enum ChatStreamEventTypes
    {
        Unknown,
        TextGeneration,
        SearchQueriesGeneration,
        SearchResults,
        StreamStart,
        StreamEnd,
        CitationGeneration
    }

    static readonly Dictionary<string, ChatStreamEventTypes> EventsMap = new Dictionary<string, ChatStreamEventTypes>
    {
        { "stream-start", ChatStreamEventTypes.StreamStart },
        { "stream-end", ChatStreamEventTypes.StreamEnd },
        { "text-generation", ChatStreamEventTypes.TextGeneration },
        { "search-queries-generation", ChatStreamEventTypes.SearchQueriesGeneration },
        { "search-results", ChatStreamEventTypes.SearchResults },
        { "citation-generation", ChatStreamEventTypes.CitationGeneration }
    };

    internal class ChatTextGenerationEventData
    {
        public string Text { get; set; }
    }

    internal class ChatStreamEventBase
    {
        [JsonProperty("is_finished")]
        public bool IsFinished { get; set; }
        [JsonProperty("event_type")]
        public string EventType { get; set; }
    }

    public override async IAsyncEnumerable<T?> InboundStream<T>(StreamReader reader) where T : class
    {
        StreamRequestTypes requestType = GetStreamType(typeof(T));
        
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.IsNullOrWhiteSpace())
            {
                continue;
            }
            
            ChatStreamEventBase? baseEvent = JsonConvert.DeserializeObject<ChatStreamEventBase>(line);

            if (baseEvent is null)
            {
                continue;
            }

            if (!EventsMap.TryGetValue(baseEvent.EventType, out ChatStreamEventTypes eventType))
            {
                continue;
            }

            if (eventType is ChatStreamEventTypes.TextGeneration)
            {
                ChatTextGenerationEventData? data = JsonConvert.DeserializeObject<ChatTextGenerationEventData>(line);

                if (data is null)
                {
                    continue;
                }

                if (requestType is StreamRequestTypes.Chat)
                {
                    yield return (T)(dynamic) new ChatResult
                    {
                        Choices = [
                            new ChatChoice
                            {
                                Delta = new ChatMessage(ChatMessageRole.Assistant, data.Text)
                            }
                        ]
                    };
                }
            }

            if (baseEvent.IsFinished)
            {
                break;
            }
        }
    }

    public override HttpRequestMessage OutboundMessage(string url, HttpMethod verb, object? data, bool streaming)
    {
        HttpRequestMessage req = new(verb, url) 
        {
            Version = OutboundVersion
        };
        req.Headers.Add("User-Agent", EndpointBase.GetUserAgent());

        ProviderAuthentication? auth = Api.GetProvider(LLmProviders.Cohere).Auth;
        
        if (auth?.ApiKey is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.ApiKey.Trim());
        }

        return req;
    }

    public override void ParseInboundHeaders<T>(T res, HttpResponseMessage response)
    {
        res.Provider = this;
    }
    
    public override T? InboundMessage<T>(string jsonData, string? postData) where T : default
    {
        if (typeof(T) == typeof(ChatResult))
        {
            return (T?)(dynamic)ChatResult.Deserialize(LLmProviders.Cohere, jsonData, postData);
        }
        
        return JsonConvert.DeserializeObject<T>(jsonData);
    }
}