using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace TwMediaChecker {
    class Program {
        private Tokens tokens;

        static void Main(string[] args) {
            (new Program()).exec(args);
        }

        private void exec(string[] args) {
            var opts = Option(args);
            foreach (var opt in opts) {
                Console.WriteLine($"{opt.Key}: {opt.Value}");
            }
            Console.WriteLine();

            if (opts.Any(value => value.Value == null)) {
                Console.WriteLine("Usage:");
                Console.WriteLine("    -key api_key");
                Console.WriteLine("    -key_secret api_secret");
                Console.WriteLine("    -token token");
                Console.WriteLine("    -token_secret token_secret");
                return;
            }
            tokens = Tokens.Create(opts["key"], opts["key_secret"], opts["token"], opts["token_secret"]);

            var exists = new List<string>();
            var notExists = new List<string>();
            var error = new List<string>();

            var regex = new Regex(@"https://twitter.com/.+/status/(?<id>.+)");

            var lines = new List<string>();
            string tmp;
            while (!string.IsNullOrEmpty(tmp = Console.ReadLine())) {
                lines.Add(tmp);
            }

            foreach (var line in lines) {
                var match = regex.Match(line);
                if (!match.Success) {
                    Console.Error.WriteLine($"parse failed: {line}");
                    error.Add(line);
                    continue;
                }

                var id = long.Parse(match.Groups["id"].Value);
                try {
                    Thread.Sleep(100);
                    Console.WriteLine($"getting tweet: {line}");
                    var tweet = tokens.Statuses.Show(id);

                    (tweet.Entities.Media == null ? notExists : exists).Add(line);
                } catch (TwitterException e) {
                    error.Add(line);
                    if (IsNotFound(e.Errors)) {
                        Console.Error.WriteLine($"not found: {line}");
                        continue;
                    }

                    foreach (var er in e.Errors) {
                        Console.Error.WriteLine($"{er.Message}: {line}");
                    }

                    Console.Error.WriteLine("Waiting...");
                    Thread.Sleep((int)(e.RateLimit.Reset.ToUnixTimeMilliseconds() - DateTime.Now.Millisecond));
                } catch (NullReferenceException e) {
                    Console.Error.WriteLine(e.StackTrace);
                }
            }
            Console.WriteLine();
            Console.WriteLine("text only:");
            notExists.ForEach(Console.WriteLine);

            Console.WriteLine();
            Console.WriteLine("media exist:");
            exists.ForEach(Console.WriteLine);

            Console.WriteLine();
            Console.WriteLine("error:");
            error.ForEach(Console.WriteLine);
        }

        private bool IsNotFound(Error[] e) {
            foreach (var er in e) {
                switch (er.Code) {
                    case 8:
                    case 34:
                    case 144:
                        return true;
                }
            }
            return false;
        }

        private Dictionary<string, string> Option(string[] args) {
            // https://qiita.com/Marimoiro/items/a090344432a5f69e1fac
            args = args.Concat(new string[] { "" }).ToArray();
            var op = new string[] { "-key", "-key_secret", "-token", "-token_secret" };
            return op.ToDictionary(p => p.Substring(1), p => args.SkipWhile(a => a != p).Skip(1).FirstOrDefault());
        }
    }
}
