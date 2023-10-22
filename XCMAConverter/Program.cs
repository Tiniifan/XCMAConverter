using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using XCMAConverter.Level5.Camera;

namespace XCMAConverter
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Options:");
                Console.WriteLine("-h: help");
                Console.WriteLine("-d [input_path] [output_path]: decompress .cmr2 to readable human text");
                Console.WriteLine("-c [input_path] [output_path]: compress readable human text to .cmr2");
                return;
            }

            if (args[0] == "-h")
            {
                Console.WriteLine("Options:");
                Console.WriteLine("-h: help");
                Console.WriteLine("-d [input_path] [output_path]: decompress .cmr2 to readable human text");
                Console.WriteLine("-c [input_path] [output_path]: compress readable human text to .cmr2");
            } 
            else if (args[0] == "-d")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Please provide input and output file names for the -d option.");
                    return;
                }

                XCMA camera = new XCMA(new FileStream(args[1], FileMode.Open, FileAccess.Read));

                // Check if the output folder exists, otherwise create it.
                string outputDirectory = Path.GetDirectoryName(args[2]);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Print camera data
                using (StreamWriter file = new StreamWriter(args[2]))
                {
                    file.WriteLine("HashName: " + camera.HashName.ToString("X8"));
                    foreach (KeyValuePair<int, Dictionary<int, float[]>> entry in camera.CamValues)
                    {
                        file.WriteLine(entry.Key + ":");
                        foreach (KeyValuePair<int, float[]> innerEntry in entry.Value)
                        {
                            file.Write("\t" + innerEntry.Key + ": (");
                            for (int i = 0; i < innerEntry.Value.Length; i++)
                            {
                                file.Write(innerEntry.Value[i].ToString(CultureInfo.InvariantCulture));
                                if (i != innerEntry.Value.Length - 1)
                                    file.Write(", ");
                            }
                            file.WriteLine(")");
                        }
                    }
                }
            }
            else if (args[0] == "-c")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Please provide input and output file names for the -c option.");
                    return;
                }

                XCMA camera = new XCMA(File.ReadAllLines(args[1]));

                // Check if the output folder exists, otherwise create it.
                string outputDirectory = Path.GetDirectoryName(args[2]);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                camera.Save(args[2]);
            }
        }
    }
}
