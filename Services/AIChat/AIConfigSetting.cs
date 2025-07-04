namespace GameApp.Services.AIChat
{
    public static class AIConfigSettings
    {
        public const string ApiEndpoint = "https://api.gptsapi.net/v1/chat/completions";

        // Replace with your actual API key.
        public const string ApiKey = " API key";

        public const string ModelName = "gpt-4o-mini";

        public const string SystemPrompt = @"You are an empathetic AI companion designed to provide emotional support.
Always respond with compassion and understanding.
If the user seems distressed, focus on validation and encouragement.
Never dismiss their feelings or give generic advice.
Be conversational but thoughtful.
Keep responses concise but helpful.
";
    }
}
