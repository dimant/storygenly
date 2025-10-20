# StoryGenly Prompt Template Variables

This document describes the context variables available for use in prompt templates.

## Standard Variables

All prompt templates have access to these basic context variables:

- `{project_name}` - Name of the current story project
- `{project_id}` - Unique identifier for the project
- `{created_at}` - When the project was created
- `{current_phase}` - Name of the current generation phase
- `{genre}` - Primary genre (typically "science fiction")
- `{tone}` - Desired story tone (e.g., "dark", "hopeful", "mysterious")
- `{target_audience}` - Intended readership level
- `{estimated_length}` - Target word count for the finished story
- `{author_preferences}` - Any specific author style preferences

## Phase Output Variables

As phases complete, their outputs become available as variables:

- `{BIBLE_DATA}` or `{bible_output}` - Complete story bible in NDJSON format
- `{CHAPTERS_DATA}` or `{chapters_output}` - Chapter outline JSON
- `{SCENES_DATA}` or `{scenes_output}` - Scene breakdown JSON
- `{STYLE_DATA}` or `{style_output}` - Style guide JSON
- `{ORIGINALITY_DATA}` or `{originality_output}` - Originality analysis JSON

## Extended Context Variables

Additional context that may be available depending on configuration:

- `{user_feedback}` - Feedback provided during revision requests
- `{revision_type}` - Type of revision being requested
- `{reference_texts}` - Relevant reference text samples from VectorDB
- `{character_profiles}` - Detailed character development data
- `{world_building}` - Extended world-building information
- `{dialogue_guide}` - Character voice and dialogue specifications
- `{content_to_revise}` - Specific content that needs revision

## Phase-Specific Variables

### Bible Phase
- No additional variables (this is the starting phase)

### Chapter Phase
- `{BIBLE_DATA}` - Story bible elements to build upon

### Scene Phase
- `{BIBLE_DATA}` - Story bible for character/location reference
- `{CHAPTERS_DATA}` - Chapter structure to break down into scenes

### Style Phase
- `{BIBLE_DATA}` - Story elements for style matching
- `{CHAPTERS_DATA}` - Story structure for style application
- `{reference_texts}` - Sample texts from similar works (via VectorDB)

### Originality Phase
- All previous phase outputs for comprehensive analysis

## Usage Guidelines

1. **Variable Naming**: Use uppercase for major phase outputs (e.g., `{BIBLE_DATA}`) and lowercase for derived variables (e.g., `{bible_output}`)

2. **Conditional Content**: Check if variables exist before using them:
   ```
   {{#if chapters_output}}
   Based on the chapter outline: {chapters_output}
   {{/if}}
   ```

3. **Variable Substitution**: The StoryEngine automatically replaces variables using the `ReplaceContextVariables` method

4. **JSON Integration**: Phase outputs are typically JSON, so structure prompts to expect properly formatted data

5. **Fallback Values**: Provide reasonable defaults when variables might be missing:
   ```
   Genre: {genre|science fiction}
   Tone: {tone|adventurous}
   ```

## Custom Variables

Projects can add custom context variables by adding them to the project's Context dictionary:

```csharp
context["custom_setting"] = "underwater colony";
context["time_period"] = "2157 CE";
context["special_constraint"] = "no faster-than-light travel";
```

These become available as `{custom_setting}`, `{time_period}`, `{special_constraint}`, etc.

## Best Practices

1. Always provide fallback content when a variable might be empty
2. Structure prompts to handle missing optional variables gracefully
3. Use descriptive variable names that clearly indicate their content
4. Consider the order of variable substitution (some variables may reference others)
5. Test prompts with minimal context to ensure they work with missing variables
6. Document any custom variables added to specific projects