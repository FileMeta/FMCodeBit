/*
---
name: Program.cs
description: FMCodeBit main entry point. Manages and updates source code bits.
...
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace FMCodeBit
{
    class Program
    {

        const string c_syntax =
@"Syntax:
   FMCodeBit [options] [operations]

Operations:
   -g <url>         Get a CodeBit from an online repository.
   -u <path>        Update existing CodeBit(s).
   -h               Present this help file

Options:
   -s               Search subdirectories when updating CodeBits.

* Operations, '-g', '-u', and '-h',  may be repeated and mixed.
* Path for '-u' operation may include wildcards.
* -s option only affects -u operations that follow (order matters).

Examples:
   FMSrcGet -g https://github.com/FileMeta/MicroYaml/raw/master/MicroYamlReader.cs
   FMSrcGet -s -u *.cs

FMCodeBit is support tool for sharing self-contained source code files known
as 'CodeBits'. The purpose is similar to that of a package managers like
NuGet however the granularity of sharing is a single source-code file.

The -g 'get' option retrieves the specified CodeBit source code file and
drops it in the current directory. Typically the source code would be
found in a repository like GitHub.

The -u 'update' option reads the metadata from the specified local CodeBits
and compares against the corresponding master copy on the web. If the master
copy is newer then it prompts the user and then replaces the local copy with
the master.

CodeBits are source code files with a metadata block near the beginning of
the file. The metadata is in MicroYaml format (a subset of YAML) and uses
metadata property definitions from Schema.org (http://schema.org). At a
minimum, the metadata block must include the 'url', 'version', and 'keywords'
properties and 'CodeBit' must appear in the keywords. Others metadata
properties are optional.

The metadata block must begin with a YAML 'Begin Document' indicator which is
a line with just three dashes. It ends with a YAML 'End Document' indicator
which is a line with just three dots. Typically the metadata block is
enclosed by comment delimiters appropriate to the programming language. Here
is a sample metadata block for a C# source code file.

/*
---
# Metadata comment
name: MySharedCode.cs
description: Shared code demonstration module
url: https://github.com/FileMeta/AcmeIndustries/raw/master/MySharedCode.cs
version: 1.4
keywords: CodeBit
dateModified: 2017-05-24
copyrightHolder: Henry Higgins
copyrightYear: 2017
license: https://opensource.org/licenses/BSD-3-Clause
...
*/
";
// Column 78                                                                 |

        static void Main(string[] args)
        {
            try
            {
                bool includeSubdirectories = false;

                // Process the command line
                if (args.Length == 0)
                {
                    Console.WriteLine("Empty command line. Enter 'FMCodeBit -h' for help.");
                }
                else
                {
                    for (int i=0; i<args.Length; ++i)
                    {
                        switch (args[i])
                        {
                            case "-h":
                            case "-H":
                                Console.WriteLine(c_syntax);
                                break;

                            case "-s":
                                includeSubdirectories = true;
                                break;

                            case "-g":
                                ++i;
                                if (i >= args.Length)
                                {
                                    Console.WriteLine("Argument not specified for option '-g'. Use '-h' for help.");
                                }
                                else
                                {
                                    GetCodeBit(args[i]);
                                }
                                break;

                            case "-u":
                                ++i;
                                if (i >= args.Length)
                                {
                                    Console.WriteLine("Argument not specified for option '-h'. Use '-h' for help.");
                                }
                                else
                                {
                                    UpdateCodeBits(args[i], includeSubdirectories);
                                }
                                break;

                            default:
                                Console.WriteLine("Syntax error: Argument '{0}' not understood. Use '-h' for help.", args[i]);
                                break;
                        }
                    }
                }
            }
            catch (Exception err)
            {
#if DEBUG
                Console.WriteLine(err.ToString());
#else
                Console.WriteLine(err.Message);
#endif
            }

            Win32Interop.ConsoleHelper.PromptAndWaitIfSoleConsole();
        }

        static void GetCodeBit(string srcUrl)
        {
            Console.WriteLine("GetCodeBit: " + srcUrl);
        }

        static void UpdateCodeBits(string filePattern, bool includeSubdirectories)
        {
            string directory = Path.GetDirectoryName(filePattern);
            directory = Path.GetFullPath(string.IsNullOrEmpty(directory) ? "." : directory);
            string pattern = Path.GetFileName(filePattern);
            Console.WriteLine("Updating CodeBits: " + Path.Combine(directory, pattern));

            // Snapshot all matches because we may modify things as we process the list.
            string[] matches = Directory.GetFiles(directory, pattern, includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            if (matches.Length == 0)
            {
                Console.WriteLine("  No matches found.");
                Console.WriteLine();
                return;
            }

            foreach(string path in matches)
            {
                UpdateCodebit(path);
            }
        }

        static void UpdateCodebit(string path)
        {
            var options = new Yaml.YamlReaderOptions();
            options.IgnoreTextOutsideDocumentMarkers = true;

            Console.WriteLine();
            Console.WriteLine(path);

            var dict = new Dictionary<string, string>();
            try
            {
                Yaml.MicroYaml.LoadFile(path, options, dict);
            }
            catch (Exception err)
            {
                Console.WriteLine("   YAML Syntax Error: " + err.Message);
                return;
            }

            WriteIfPresent(dict, "name");
            WriteIfPresent(dict, "description");

            if (!HasKeyword(dict, "CodeBit"))
            {
                Console.WriteLine("   Not a CodeBit.");
                return;
            }

            string localVersion;
            if (!dict.TryGetValue("version", out localVersion))
            {
                Console.WriteLine("   Error: CodeBit missing version property.");
                return;
            }
            Console.WriteLine("   version: {0}", localVersion);

            string url;
            if (!dict.TryGetValue("url", out url))
            {
                Console.WriteLine("   Error: CodeBit missing url property.");
                return;
            }
            Console.WriteLine("   url: {0}", url);

            Console.WriteLine("   Retrieving master copy...");
            string tempFileName = null;
            try
            {
                if (!HttpGetToTempFile(url, Path.GetDirectoryName(path), out tempFileName)) return;

                var remoteDict = new Dictionary<string, string>();
                try
                {
                    Yaml.MicroYaml.LoadFile(tempFileName, options, remoteDict);
                }
                catch (Exception err)
                {
                    Console.WriteLine("   YAML Syntax Error on master copy: " + err.Message);
                    return;
                }

                if (!HasKeyword(remoteDict, "CodeBit"))
                {
                    Console.WriteLine("   Master Copy is not a CodeBit.");
                    return;
                }

                if (!remoteDict.ContainsKey("url"))
                {
                    Console.WriteLine("    Master Copy is missing 'url' property.");
                    return;
                }

                string remoteVersion;
                if (!remoteDict.TryGetValue("version", out remoteVersion))
                {
                    Console.WriteLine("   Error: Master CodeBit missing 'version' property.");
                    return;
                }

                int vercomp = CompareMixedNumeric(remoteVersion, localVersion);
                if (vercomp == 0)
                {
                    Console.WriteLine("   Local CodeBit is up to date!");
                    return;
                }
                if (vercomp < 0)
                {
                    Console.WriteLine("   Unexpected: Local CodeBit has newer version than master copy.");
                    Console.WriteLine("   Local version: '{0}'  Master version: '{1}'", localVersion, remoteVersion);
                    return;
                }

                Console.Write("   Master version is newer. Update local copy to version '{0}' (Y/N)? ", remoteVersion);
                char c = Console.ReadKey().KeyChar;
                if (c == 'y' || c == 'Y')
                {
                    Console.WriteLine(" (Yes)");
                    File.Delete(path);
                    File.Move(tempFileName, path);
                    tempFileName = null;
                    Console.WriteLine("   '{0}' updated to version '{1}'.", Path.GetFileName(path), remoteVersion);
                }
                else
                {
                    Console.WriteLine(" (No)");
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFileName) && File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
            }

        }

        static bool WriteIfPresent(IReadOnlyDictionary<string, string> dict, string key)
        {
            string value;
            if (!dict.TryGetValue(key, out value)) return false;
            Console.WriteLine(string.Format("   {0}: {1}", key, value));
            return true;
        }

        static bool HasKeyword(IReadOnlyDictionary<string, string> dict, string keyword)
        {
            string keywords;
            if (!dict.TryGetValue("keywords", out keywords))
                return false;

            foreach (var candidate in keywords.Split(',', ';'))
            {
                if (candidate.Trim(' ', '\t', '#').Equals(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compares two strings. When a numeric segment is encountered uses
        /// numeric order (as if the shorter number is padded with leading zeros).
        /// When a non-numeric segment is encountered, uses Unicode order.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static int CompareMixedNumeric(string a, string b)
        {
            // Get cursors and limits
            int ia = 0;
            int ib = 0;
            int la = a.Length;
            int lb = b.Length;
            for (;;)
            {
                // if exceeded length of either string, exit
                if (ia >= la)
                {
                    if (ib >= lb)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
                if (ib >= lb)
                {
                    return -1;
                }

                // Get the next character of each string
                char ca = a[ia];
                char cb = b[ib];

                // If both characters are digits, compare numerically
                if (char.IsDigit(ca) && char.IsDigit(cb))
                {
                    // Scan to the end of each numeric segment
                    int xa = ia;
                    ++ia;
                    while (ia < la && char.IsDigit(a[ia])) ++ia;
                    int xb = ib;
                    ++ib;
                    while (ib < lb && char.IsDigit(b[ib])) ++ib;

                    // Scan leading digits
                    while (ia-xa > ib-xb)
                    {
                        if (a[xa] != '0') return 1;
                        ++xa;
                    }
                    while (ib-xb > ia-xa)
                    {
                        if (b[xb] != '0') return -1;
                        ++xb;
                    }
                    while (xa < ia)
                    {
                        if (a[xa] > b[xb]) return 1;
                        if (b[xb] > a[xa]) return -1;
                        ++xa;
                        ++xb;
                    }
                }

                // Return if the characters don't match.
                else if (ca > cb)
                {
                    return 1;
                }
                else if (cb > ca)
                {
                    return -1;
                }
                ++ia;
                ++ib;
            }
        }

#if DEBUG
        static void TestCompareMixedNumeric()
        {
            TestCompareMixedNumeric("1234", "1234");
            TestCompareMixedNumeric("1234", "0001234");
            TestCompareMixedNumeric("1234", "000000020");
            TestCompareMixedNumeric("0005", "10");
            TestCompareMixedNumeric("10", "8");
            TestCompareMixedNumeric("Of5Ten", "Of10Able");
            TestCompareMixedNumeric("1.3.5", "2.0.0");
            TestCompareMixedNumeric("1.30.5", "2.0.0");
            TestCompareMixedNumeric("1.30.5", "1.8.5");
            TestCompareMixedNumeric("10.1.5", "10.01.0005");
        }

        static void TestCompareMixedNumeric(string a, string b)
        {
            int n = CompareMixedNumeric(a, b);
            char c = (n < 0) ? '<' : ((n > 0) ? '>' : '=');
            Console.WriteLine("'{0}' {1} '{2}'", a, c, b);
        }
#endif

        static bool HttpGetToTempFile(string url, string workingDirectory, out string tempFilename)
        {
            string filename = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                filename = Path.Combine(workingDirectory, Path.GetRandomFileName() + ".tmp");
                using (Stream outStream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    using (Stream inStream = response.GetResponseStream())
                    {
                        inStream.CopyTo(outStream);
                    }
                }

                tempFilename = filename;
                filename = null;
                return true;
            }
            catch (WebException err)
            {
                HttpWebResponse response = err.Response as HttpWebResponse;
                if (response != null)
                {
                    Console.WriteLine("   Web Error: {0} {1}", (int)response.StatusCode, response.StatusDescription);
                }
                else
                {
                    Console.WriteLine("   Web Error: {0}", err.Message);
                }
            }
            catch (Exception err)
            {
                Console.WriteLine("   Web Error: {0}", err.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
                {
                    File.Delete(filename);
                }
            }

            tempFilename = null;
            return false;
        }

    }
}
