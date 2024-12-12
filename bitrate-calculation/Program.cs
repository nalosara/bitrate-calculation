using System;
using System.Collections.Generic;
using bitrate_exercise;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace bitrate_exercise
{
    public class Program
    {
        static void Main(string[] args)
        {
            string json = @"{
            ""Device"": ""Arista"",
            ""Model"": ""X-Video"",
            ""NIC"": [{
                ""Description"": ""Linksys ABR"",
                ""MAC"": ""14:91:82:3C:D6:7D"",
                ""Timestamp"": ""2020-03-23T18:25:43.511Z"",
                ""Rx"": ""3698574500"",
                ""Tx"": ""122558800""
            }]}";

            ValidateNicData(json);

            var data = JsonSerializer.Deserialize<Data>(json);
            List<NIC> samples = GenerateSamples(data.NIC[0], 5, 0.5);

            CalculateBitrate(json, samples, 0.5);
        }

        // validating that the data needed is in the json that is received
        public static void ValidateNicData(string json)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Data>(json);

                if (data?.NIC == null)
                {
                    throw new InvalidOperationException("No NIC data found in the JSON.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating NIC data: {ex.Message}");
                throw;
            }
        }

        // generating additional samples with adjustments to the timestamp and both the receive and transmission bitrate

        public static List<NIC> GenerateSamples(NIC baseSample, int count, double pollingRate)
        {
            var samples = new List<NIC> { baseSample };
            Random random = new Random();

            for (int i = 1; i < count; i++)
            {
                NIC prevSample = samples[i - 1];
                NIC newSample = new NIC
                {
                    Description = prevSample.Description,
                    MAC = prevSample.MAC,
                    Timestamp = DateTime.Parse(prevSample.Timestamp).AddSeconds(pollingRate).ToString("o"),
                    Rx = (long.Parse(prevSample.Rx) + random.Next(100000, 500000)).ToString(),
                    Tx = (long.Parse(prevSample.Tx) + random.Next(100000, 500000)).ToString()
                };
                samples.Add(newSample);
            }

            return samples;
        }


        public static void CalculateBitrate(string json, List<NIC> generatedSamples, double pollingRate)
        {
            // values used to keep track of previous, the first sample is skipped because there is no previous one

            long previousRx = 0;
            long previousTx = 0;
            DateTime? previousTimestamp = null;
            bool isFirstSample = true;
            double epsilon = 1e-6;

            foreach (var nic in generatedSamples)
            {
                // parsing te values retreived from the samples
                long currentRx = long.Parse(nic.Rx);
                long currentTx = long.Parse(nic.Tx);

                DateTime timestamp = DateTime.Parse(nic.Timestamp);

                // calucating the time difference between the previous and the current
                double timeDelta = 0;
                if (previousTimestamp.HasValue)
                {
                    timeDelta = (timestamp - previousTimestamp.Value).TotalSeconds;
                }

                if (!isFirstSample)
                {
                    // checking for irregular polling intervals (when the time interval is not as expected, i.e. 0.6)
                    if (Math.Abs(timeDelta - 0.5) > epsilon)
                    {
                        Console.WriteLine($"Irregular polling at {timestamp}. Time delta: {timeDelta:F3}s");
                    }

                    long rxDelta = currentRx - previousRx;
                    long txDelta = currentTx - previousTx;

                    // counter rollover (if the calucalted values are negative, assume that the rollover happend - exceeded the maximum value)
                    if (rxDelta < 0)
                    {
                        rxDelta += long.MaxValue;
                        Console.WriteLine("Warning: Counter rollover detected for Rx.");
                    }

                    if (txDelta < 0)
                    {
                        txDelta += long.MaxValue;
                        Console.WriteLine("Warning: Counter rollover detected for Tx.");
                    }

                    // spikes - unexpected spikes in values
                    if (rxDelta > 1e9 || txDelta > 1e9)
                    {
                        Console.WriteLine($"Spike at {timestamp}. RxDelta: {rxDelta}, TxDelta: {txDelta}");
                        continue;
                    }

                    // negative delta check if its not rollover
                    if (rxDelta < epsilon || txDelta < epsilon)
                    {
                        Console.WriteLine($"Timestamp: {timestamp}");
                        Console.WriteLine("Negative delta detected. Skipping calculation.");
                        continue;
                    }

                    // calculating the bitrates
                    double rxBitrate = (rxDelta * 8) / pollingRate;
                    double txBitrate = (txDelta * 8) / pollingRate;

                    Console.WriteLine($"RX Bitrate: {rxBitrate:F2} bps");
                    Console.WriteLine($"TX Bitrate: {txBitrate:F2} bps");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"First sample at {timestamp}. Skipping bitrate calculation.");
                    isFirstSample = false;
                }

                previousRx = currentRx;
                previousTx = currentTx;
                previousTimestamp = timestamp;
            }
        }

    }
}