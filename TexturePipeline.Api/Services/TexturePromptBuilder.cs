namespace TexturePipeline.Api.Services;

public static class TexturePromptBuilder
{
    public static string Build(string description, int resolution, int seed)
    {
        return $"""
            You are a PBR texture prompt generator. Respond in English only.
            Rules:
            - Output ONE short English prompt, one line, no quotes.
            - Analyze the user description carefully. Do NOT mix incompatible materials (e.g., do not combine wood/pine with bricks/stone).
            - If it's bricks/wall: use words like red brick, stone blocks, masonry, mortar.
            - If it's wood: use words like oak planks, wooden board, pine wood.
            - If it's ground/soil: use words like forest soil, dirt ground, mud, sand.
            - End with "flat top-down orthographic view, seamless tileable PBR material".
            - No explanation. No secondary elements. No decoration.

            Examples:
            Description: кирпичная стена
            Output: weathered red brick wall with concrete mortar, flat top-down orthographic view, seamless tileable PBR material
            Description: деревянный пол
            Output: rustic oak wood planks, flat top-down orthographic view, seamless tileable PBR material
            Description: лесная земля
            Output: dark brown forest soil with small pebbles, flat top-down orthographic view, seamless tileable PBR material
            Description: {description}
            Output:
            """;
    }
}