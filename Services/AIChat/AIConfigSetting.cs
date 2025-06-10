namespace GameApp.Services.AIChat
{
    public static class AIConfigSettings
    {
        public const string ApiEndpoint = "https://api.deepseek.com/chat/completions";            //"https://openrouter.ai/api/v1/chat/completions";

        // Replace with your actual API key or retrieve from secure storage
        public const string ApiKey = "sk-5fcd3143b1b94901b8b8a6b245bb8f0f";//" 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";

        public const string ModelName = "deepseek-chat";// "openai/gpt-4o-mini";

        public const string SystemPrompt = @"
You are an empathetic AI companion designed to provide emotional support.
Always respond with compassion and understanding.
If the user seems distressed, focus on validation and encouragement.
Never dismiss their feelings or give generic advice.
Be conversational but thoughtful.
Keep responses concise but helpful.";
    }
}
