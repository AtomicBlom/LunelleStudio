using BinaryVibrance.Lunelle.Music;
using BinaryVibrance.LunelleStudio;

namespace BinaryVibrance.LunelleStudio.Tests;

/// <summary>The plays-when editor's model: a condition taken apart by cue, put back together, said in words, and checked now.</summary>
public class ConditionShapeTests
{
	private enum Place { Coast, Plains, Forest, Mountains }

	private static readonly Cue Intensity = new("intensity");
	private static readonly Cue<Place> Biome = new("biome");
	private static readonly IReadOnlyList<CueBase> Cues = [Intensity, Biome];

	private static Condition Parse(string text) => ConditionText.Parse(text, name => Cues.FirstOrDefault(c => c.Name == name));

	private static ConditionShape Shape(string? text) => ConditionShape.Read(text is null ? null : Parse(text), Cues).ShouldNotBeNull();

	[Test]
	[Arguments("intensity > 0.45")]
	[Arguments("intensity < 0.22")]
	[Arguments("intensity > 0.3 and intensity < 0.7")]
	[Arguments("biome is Mountains")]
	[Arguments("(biome is Mountains or biome is Forest) and intensity > 0.5")]
	[Arguments("not biome is Forest")]
	[Arguments("not (biome is Coast or biome is Plains)")]
	[Arguments("intensity > 0.5 and pass < 2")]
	[Arguments("pass > 1")]
	[Arguments("pass is 3")]
	public void A_condition_the_clauses_can_hold_comes_back_as_it_means(string text)
	{
		var shape = Shape(text);
		var written = shape.Write().ShouldNotBeNull();
		// The same condition again, however it is ordered or bracketed: it holds wherever the first does
		var again = Parse(written);
		var first = Parse(text);
		foreach (var level in new[] { 0f, 0.2f, 0.45f, 0.6f, 0.9f })
			foreach (var place in Enum.GetValues<Place>())
				foreach (var pass in new[] { 1, 2, 3 })
				{
					float Value(CueBase cue) => cue == Intensity ? level : (float)place;
					again.IsMet(Value, pass).ShouldBe(first.IsMet(Value, pass), $"{written} against {text} at {level}, {place}, pass {pass}");
				}
	}

	[Test]
	[Arguments("intensity > 0.5 or biome is Forest")]
	[Arguments("intensity > 0.5 and intensity > 0.6")]
	[Arguments("not intensity > 0.5")]
	[Arguments("pass < 3")]
	public void A_condition_joined_some_other_way_has_no_shape_and_stays_text(string text) =>
		ConditionShape.Read(Parse(text), Cues).ShouldBeNull();

	[Test]
	public void Always_is_an_empty_shape_that_writes_nothing()
	{
		var shape = Shape(null);
		shape.IsAlways.ShouldBeTrue();
		shape.Write().ShouldBeNull();
		shape.Sentence().ShouldBe("Plays always.");
		shape.Now(_ => 0f, 1).Met.ShouldBeTrue();
	}

	[Test]
	public void Its_clauses_are_said_in_a_sentence_and_the_first_that_fails_says_why_it_is_silent()
	{
		var shape = Shape("intensity > 0.45 and (biome is Mountains or biome is Forest) and pass < 2");
		shape.Sentence().ShouldBe("Plays when intensity is at or above 0.45, biome is Forest or Mountains and on its first pass.");
		shape.Level(Intensity)!.Rule.ShouldBe(LevelRule.Above);
		shape.Choice(Biome)!.Values.ShouldBe([(float)Place.Forest, (float)Place.Mountains]);
		shape.Pass.ShouldBe(PassRule.First);

		float At(CueBase cue, float level, Place place) => cue == Intensity ? level : (float)place;
		shape.Now(c => At(c, 0.38f, Place.Forest), 1).ShouldBe((false, "intensity 0.38 is under 0.45"));
		shape.Now(c => At(c, 0.6f, Place.Coast), 1).ShouldBe((false, "biome is Coast, not Forest or Mountains"));
		shape.Now(c => At(c, 0.6f, Place.Forest), 2).ShouldBe((false, "this is pass 2"));
		shape.Now(c => At(c, 0.6f, Place.Forest), 1).Met.ShouldBeTrue();
	}

	[Test]
	public void A_level_changed_by_the_editor_is_written_to_hundredths_and_between_two_levels_is_two_comparisons()
	{
		var shape = Shape("intensity > 0.45");
		var level = shape.Level(Intensity)!;
		level.Rule = LevelRule.Between;
		level.From = 0.3049f;
		level.To = 0.7f;
		shape.Write().ShouldBe("intensity > 0.3 and intensity < 0.7");

		var choice = shape.Choice(Biome)!;
		choice.Values.Add((float)Place.Coast);
		choice.Except = true;
		shape.Write().ShouldBe("intensity > 0.3 and intensity < 0.7 and not biome is Coast");
		shape.Sentence().ShouldBe("Plays when intensity is between 0.3 and 0.7 and biome is anything but Coast.");
	}
}
