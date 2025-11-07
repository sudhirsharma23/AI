using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace TextractProcessor.Tools
{
    /// <summary>
    /// Helper class to generate training data for AWS Bedrock model fine-tuning
    /// </summary>
    public class TrainingDataGenerator
    {
        private readonly List<TrainingExample> _examples = new();

        public class TrainingExample
        {
            public string Prompt { get; set; }
            public string Completion { get; set; }
        }

        /// <summary>
        /// Add a training example with source text and expected JSON output
        /// </summary>
        public void AddExample(string sourceText, string targetSchema, string expectedJsonOutput)
        {
            var prompt = $"Transform the following source data into a JSON object that matches the target schema structure.\n\n" +
        $"Source Data:\n{sourceText}\n\n" +
    $"Target Schema:\n{targetSchema}\n\n" +
       $"Rules:\n" +
   $"- Create a valid JSON object\n" +
                $"- Use proper data types\n" +
       $"- Distribute ownership percentages equally among all owners\n" +
   $"- Set buyerIsPrimary to true for first owner only\n\n" +
    $"Output Format: Return only the JSON object.";

            _examples.Add(new TrainingExample
            {
                Prompt = prompt,
                Completion = expectedJsonOutput
            });
        }

        /// <summary>
        /// Generate sample training examples for common scenarios
        /// </summary>
        public void GenerateSampleExamples()
        {
            // Example 1: Two owners - Husband and Wife
            AddExample(
      sourceText: "GRANT DEED from Charles D. Shapiro and Suzanne D. Shapiro, husband and wife as joint tenants to Charles David Shapiro and Suzanne Denise Shapiro, as co-trustees of the Shapiro Family Trust",
        targetSchema: "{\"buyer_names_component\": [{\"firstName\": \"\", \"middleName\": \"\", \"lastName\": \"\", \"buyerPercentage\": 0, \"buyerIsPrimary\": false}]}",
         expectedJsonOutput: @"{
  ""buyer_names_component"": [
    {
      ""firstName"": ""Charles"",
      ""middleName"": ""David"",
      ""lastName"": ""Shapiro"",
      ""buyerVesting"": ""AS CO-TRUSTEES OF THE SHAPIRO FAMILY TRUST"",
      ""buyerPercentage"": 50,
      ""buyerIsPrimary"": true
    },
    {
      ""firstName"": ""Suzanne"",
      ""middleName"": ""Denise"",
      ""lastName"": ""Shapiro"",
      ""buyerVesting"": ""AS CO-TRUSTEES OF THE SHAPIRO FAMILY TRUST"",
      ""buyerPercentage"": 50,
      ""buyerIsPrimary"": false
    }
  ]
}"
   );

            // Example 2: Three owners
            AddExample(
      sourceText: "Property granted to John Smith, Mary Johnson, and Robert Williams as joint tenants",
      targetSchema: "{\"buyer_names_component\": [{\"firstName\": \"\", \"lastName\": \"\", \"buyerPercentage\": 0, \"buyerIsPrimary\": false}]}",
     expectedJsonOutput: @"{
  ""buyer_names_component"": [
    {
      ""firstName"": ""John"",
      ""lastName"": ""Smith"",
      ""buyerVesting"": ""AS JOINT TENANTS"",
      ""buyerPercentage"": 33.33,
      ""buyerIsPrimary"": true
  },
    {
      ""firstName"": ""Mary"",
      ""lastName"": ""Johnson"",
      ""buyerVesting"": ""AS JOINT TENANTS"",
      ""buyerPercentage"": 33.33,
  ""buyerIsPrimary"": false
    },
    {
      ""firstName"": ""Robert"",
      ""lastName"": ""Williams"",
      ""buyerVesting"": ""AS JOINT TENANTS"",
      ""buyerPercentage"": 33.34,
    ""buyerIsPrimary"": false
    }
  ]
}"
            );

            // Example 3: Single owner
            AddExample(
  sourceText: "Property granted to Jennifer L. Davis, a single woman",
     targetSchema: "{\"buyer_names_component\": [{\"firstName\": \"\", \"lastName\": \"\", \"buyerPercentage\": 0, \"buyerIsPrimary\": false}]}",
    expectedJsonOutput: @"{
  ""buyer_names_component"": [
    {
      ""firstName"": ""Jennifer"",
      ""middleName"": ""L."",
      ""lastName"": ""Davis"",
      ""buyerVesting"": ""A SINGLE WOMAN"",
      ""buyerPercentage"": 100,
      ""buyerIsPrimary"": true
    }
  ]
}"
 );

            // Example 4: Four owners - Corporation
            AddExample(
    sourceText: "Property granted to Michael Chen, Lisa Martinez, David Brown, and Sarah Wilson as tenants in common",
   targetSchema: "{\"buyer_names_component\": [{\"firstName\": \"\", \"lastName\": \"\", \"buyerPercentage\": 0, \"buyerIsPrimary\": false}]}",
     expectedJsonOutput: @"{
  ""buyer_names_component"": [
    {
      ""firstName"": ""Michael"",
      ""lastName"": ""Chen"",
      ""buyerVesting"": ""AS TENANTS IN COMMON"",
      ""buyerPercentage"": 25,
      ""buyerIsPrimary"": true
    },
    {
      ""firstName"": ""Lisa"",
      ""lastName"": ""Martinez"",
      ""buyerVesting"": ""AS TENANTS IN COMMON"",
      ""buyerPercentage"": 25,
      ""buyerIsPrimary"": false
    },
    {
      ""firstName"": ""David"",
    ""lastName"": ""Brown"",
 ""buyerVesting"": ""AS TENANTS IN COMMON"",
      ""buyerPercentage"": 25,
      ""buyerIsPrimary"": false
    },
    {
      ""firstName"": ""Sarah"",
      ""lastName"": ""Wilson"",
      ""buyerVesting"": ""AS TENANTS IN COMMON"",
   ""buyerPercentage"": 25,
      ""buyerIsPrimary"": false
    }
  ]
}"
 );
        }

        /// <summary>
        /// Export training data to JSONL format for Bedrock fine-tuning
        /// </summary>
        public void ExportToJsonL(string outputPath)
        {
            using var writer = new StreamWriter(outputPath);

            foreach (var example in _examples)
            {
                var jsonLine = JsonSerializer.Serialize(new
                {
                    prompt = example.Prompt,
                    completion = example.Completion
                });
                writer.WriteLine(jsonLine);
            }

            Console.WriteLine($"Exported {_examples.Count} training examples to {outputPath}");
        }

        /// <summary>
        /// Export training data in a human-readable format for review
        /// </summary>
        public void ExportToReadableFormat(string outputPath)
        {
            using var writer = new StreamWriter(outputPath);

            writer.WriteLine("========================================");
            writer.WriteLine("TRAINING DATA FOR BEDROCK FINE-TUNING");
            writer.WriteLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            writer.WriteLine($"Total Examples: {_examples.Count}");
            writer.WriteLine("========================================\n");

            for (int i = 0; i < _examples.Count; i++)
            {
                writer.WriteLine($"--- Example {i + 1} ---");
                writer.WriteLine("\nPROMPT:");
                writer.WriteLine(new string('-', 80));
                writer.WriteLine(_examples[i].Prompt);
                writer.WriteLine("\nEXPECTED COMPLETION:");
                writer.WriteLine(new string('-', 80));
                writer.WriteLine(_examples[i].Completion);
                writer.WriteLine("\n" + new string('=', 80) + "\n");
            }

            Console.WriteLine($"Exported readable format to {outputPath}");
        }

        /// <summary>
        /// Validate training data quality
        /// </summary>
        public ValidationReport ValidateTrainingData()
        {
            var report = new ValidationReport();

            foreach (var example in _examples)
            {
                // Check if completion is valid JSON
                try
                {
                    JsonDocument.Parse(example.Completion);
                }
                catch
                {
                    report.InvalidJsonExamples++;
                }

                // Check if prompt is not empty
                if (string.IsNullOrWhiteSpace(example.Prompt))
                {
                    report.EmptyPrompts++;
                }

                // Check if completion contains percentage distribution
                if (example.Completion.Contains("buyerPercentage"))
                {
                    // Extract all percentages and verify they sum to 100 (or close to it)
                    var percentages = System.Text.RegularExpressions.Regex.Matches(
                    example.Completion,
                  @"""buyerPercentage"":\s*(\d+(?:\.\d+)?)"
                  ).Select(m => double.Parse(m.Groups[1].Value)).ToList();

                    var sum = percentages.Sum();
                    if (Math.Abs(sum - 100) > 0.5) // Allow 0.5% tolerance for rounding
                    {
                        report.InvalidPercentageDistributions++;
                    }
                }
            }

            report.TotalExamples = _examples.Count;
            report.ValidExamples = _examples.Count - report.InvalidJsonExamples - report.EmptyPrompts;

            return report;
        }

        public class ValidationReport
        {
            public int TotalExamples { get; set; }
            public int ValidExamples { get; set; }
            public int InvalidJsonExamples { get; set; }
            public int EmptyPrompts { get; set; }
            public int InvalidPercentageDistributions { get; set; }

            public void PrintReport()
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("TRAINING DATA VALIDATION REPORT");
                Console.WriteLine("========================================");
                Console.WriteLine($"Total Examples:             {TotalExamples}");
                Console.WriteLine($"Valid Examples: {ValidExamples} ({(double)ValidExamples / TotalExamples * 100:F1}%)");
                Console.WriteLine($"Invalid JSON Examples:   {InvalidJsonExamples}");
                Console.WriteLine($"Empty Prompts:      {EmptyPrompts}");
                Console.WriteLine($"Invalid Percentage Distributions:  {InvalidPercentageDistributions}");
                Console.WriteLine("========================================\n");

                if (ValidExamples == TotalExamples)
                {
                    Console.WriteLine("✓ All examples are valid! Ready for fine-tuning.");
                }
                else
                {
                    Console.WriteLine("⚠ Some examples have issues. Please review and fix before fine-tuning.");
                }
            }
        }
    }

    // Example usage program
    public class Program
    {
        public static void Main(string[] args)
        {
            var generator = new TrainingDataGenerator();

            // Generate sample examples
            generator.GenerateSampleExamples();

            // TODO: Add your own real examples here
            // generator.AddExample(sourceText, targetSchema, expectedJsonOutput);

            // Validate the data
            var validationReport = generator.ValidateTrainingData();
            validationReport.PrintReport();

            // Export for fine-tuning
            var outputDir = "TrainingData";
            Directory.CreateDirectory(outputDir);

            generator.ExportToJsonL(Path.Combine(outputDir, "bedrock_training_data.jsonl"));
            generator.ExportToReadableFormat(Path.Combine(outputDir, "training_data_readable.txt"));

            Console.WriteLine("\n✓ Training data generation complete!");
            Console.WriteLine($"\nNext steps:");
            Console.WriteLine($"1. Review the files in the '{outputDir}' folder");
            Console.WriteLine($"2. Add more real examples from your actual documents");
            Console.WriteLine($"3. Upload 'bedrock_training_data.jsonl' to S3");
            Console.WriteLine($"4. Create fine-tuning job in AWS Bedrock console");
            Console.WriteLine($"5. See BEDROCK_MODEL_TRAINING_GUIDE.md for detailed instructions");
        }
    }
}
