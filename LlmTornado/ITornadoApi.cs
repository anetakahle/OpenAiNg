using LlmTornado.Chat;
using LlmTornado.Code;
using LlmTornado.Code.Models;
using LlmTornado.Completions;
using LlmTornado.Embedding;
using LlmTornado.Files;
using LlmTornado.Images;
using LlmTornado.Models;
using LlmTornado.Moderation;

namespace LlmTornado;

/// <summary>
///     An interface for <see cref="TornadoApi" />, for ease of mock testing, etc
/// </summary>
public interface ITornadoApi
{
	/// <summary>
	///     Base url for OpenAI
	///     for OpenAI, should be "https://api.openai.com/{0}/{1}"
	///     for Azure, should be
	///     "https://(your-resource-name.openai.azure.com/openai/deployments/(deployment-id)/{1}?api-version={0}"
	/// </summary>
	string ApiUrlFormat { get; set; }

	/// <summary>
	///     Version of the Rest Api
	/// </summary>
	string ApiVersion { get; set; }

	/// <summary>
	///     Text generation in the form of chat messages. This interacts with the ChatGPT API.
	/// </summary>
	IChatEndpoint Chat { get; }

	/// <summary>
	///     Classify text against the OpenAI Content Policy.
	/// </summary>
	IModerationEndpoint Moderation { get; }

	/// <summary>
	///     Text generation is the core function of the API. You give the API a prompt, and it generates a completion. The way
	///     you “program” the API to do a task is by simply describing the task in plain english or providing a few written
	///     examples. This simple approach works for a wide range of use cases, including summarization, translation, grammar
	///     correction, question answering, chatbots, composing emails, and much more (see the prompt library for inspiration).
	/// </summary>
	ICompletionEndpoint Completions { get; }

	/// <summary>
	///     The API lets you transform text into a vector (list) of floating point numbers. The distance between two vectors
	///     measures their relatedness. Small distances suggest high relatedness and large distances suggest low relatedness.
	/// </summary>
	IEmbeddingEndpoint Embeddings { get; }

	/// <summary>
	///     The API endpoint for querying available Engines/models
	/// </summary>
	IModelsEndpoint Models { get; }

	/// <summary>
	///     The API lets you do operations with files. You can upload, delete or retrieve files. Files can be used for
	///     fine-tuning, search, etc.
	/// </summary>
	IFilesEndpoint Files { get; }

	/// <summary>
	///     The API lets you do operations with images. You can Given a prompt and/or an input image, the model will generate a
	///     new image.
	/// </summary>
	IImageGenerationEndpoint ImageGenerations { get; }

	/// <summary>
	/// Returns a concrete implementation of endpoint provider for a given known provider.
	/// </summary>
	/// <param name="provider"></param>
	/// <returns></returns>
	public IEndpointProvider GetProvider(LLmProviders provider);

	/// <summary>
	/// Returns a concrete implementation of endpoint provider for a given known model.
	/// </summary>
	/// <param name="model"></param>
	/// <returns></returns>
	public IEndpointProvider GetProvider(IModel model);
}