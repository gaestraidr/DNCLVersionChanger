using System;
using System.IO;
using System.Text;
using zlib;


namespace DNCLVersionChanger
{
    internal class Program
    {
        private static bool exitSignal = false;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate {
                exitSignal = true;
            };

            Intro();

            Console.Write("Please enter path to a Resource Pak File: [Resource00.pak] ");
            string prompt = Console.ReadLine().Replace('"', ' ').Trim();
            string PakPath = string.IsNullOrEmpty(prompt) ? "Resource00.pak" : prompt;

            if (!File.Exists(PakPath))
            {
                Console.WriteLine("File {0} does not exist...", PakPath);
                ExitPrompt();
                return;
            }

            // Open file
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(PakPath);
            FileStream fileStream = new FileStream(PakPath, FileMode.Open);
            if (fileStream.Length < 1024L)
            {
                Console.WriteLine("Corrupted filesize for a .pak file [{0} byte] ...", fileStream.Length);
                ExitPrompt();
                return;
            }

            // Seek header
            BinaryReader binaryReader = new BinaryReader(fileStream);
            byte[] array = new byte[256];
            fileStream.Seek(0L, SeekOrigin.Begin);
            fileStream.Read(array, 0, 256);
            if (Clear(DEncoding.GetString(array).ToString()) != "EyedentityGames Packing File 0.1")
            {
                Console.WriteLine("Header string [EyedentityGames Packing File 0.1] not found...");
                ExitPrompt();
                return;
            }

            binaryReader.BaseStream.Seek(4L, SeekOrigin.Current);
            int num = binaryReader.ReadInt32();
            int num2 = binaryReader.ReadInt32();
            fileStream.Seek((long)num2, SeekOrigin.Begin);

            Console.WriteLine("\nSearching for version.cfg in the {0}.pak ...", fileNameWithoutExtension);
            long position = fileStream.Position; bool verFound = false;
            for (int i = 0; i < num; i++)
            {
                fileStream.Seek(position, SeekOrigin.Begin);
                fileStream.Read(array, 0, 256);
                string text = Clear(DEncoding.GetString(array));
                int num3 = binaryReader.ReadInt32();
                binaryReader.ReadInt32();
                fileStream.Seek(4L, SeekOrigin.Current);
                int num4 = binaryReader.ReadInt32();

                fileStream.Seek(44L, SeekOrigin.Current);
                position = fileStream.Position;

                if (text.ToLower().Contains("version.cfg"))
                {
                    byte[] array2 = new byte[num3];
                    fileStream.Seek((long)num4, SeekOrigin.Begin);
                    fileStream.Read(array2, 0, num3);

                    Console.Write("Version.cfg found! Insert new version to replace: ");
                    int version = 0;
                    while (!exitSignal && !int.TryParse(Console.ReadLine(), out version))
                    {
                        Console.WriteLine("Please input only a number.");
                        Console.Write("Enter Version Number: ");
                    }
                    if (!exitSignal)
                    {
                        var zOutput = ChangeFileVersion(array2, version);
                        fileStream.Seek((long)num4, SeekOrigin.Begin);
                        fileStream.Write(zOutput, 0, num3);
                    }

                    Console.WriteLine("\nSuccessfully changed version to {0}!", version);
                    verFound = true;
                    break;
                }
            }
            if (!verFound)
                Console.WriteLine(String.Format("Version.cfg cant be found within the {0}.pak ...", fileNameWithoutExtension));

            binaryReader.Close();
            fileStream.Close();
            fileStream.Dispose();

            ExitPrompt();
        }

        private static Encoding DEncoding = Encoding.Default;

        private static string Clear(string str)
        {
            int num = str.IndexOf(Convert.ToChar(0));
            string result;
            if (num > 0)
            {
                result = str.Remove(num);
            }
            else
            {
                result = str;
            }
            return result;
        }

        private static void ExitPrompt()
        {
            Console.WriteLine("\n\nPress any key to exit....");
            Console.ReadKey();
        }

        public static void CopyStream(Stream input, Stream output)
        {
            input.Seek(0, SeekOrigin.Begin);
            byte[] buffer = new byte[2000];
            int count;
            while ((count = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, count);
            }
            output.Flush();
        }

        private static byte[] Decode(byte[] packet)
        {
            var i = packet.Length - 1;
            while (packet[i] == 0)
            {
                --i;
            }
            var temp = new byte[i + 1];
            Array.Copy(packet, temp, i + 1);

            return temp;
        }

        private static int GetVersionInt(byte[] bVer)
        {
            int start = 8;
            int end = 0;
            while (bVer[start++] != 0x0D)
            {
                end++;
            }

            byte[] temp = new byte[end];
            Array.Copy(bVer, 8, temp, 0, end);
            return int.Parse(DEncoding.GetString(temp));
        }

        // Token: 0x06000015 RID: 21 RVA: 0x000034FC File Offset: 0x000016FC
        private static byte[] ChangeFileVersion(byte[] inByte, int version)
        {
            MemoryStream inputStream = new MemoryStream(inByte);
            MemoryStream outputStream = new MemoryStream();
            ZOutputStream zOutputStream = new ZOutputStream(outputStream);

            MemoryStream compStream = new MemoryStream();
            ZOutputStream zCompStream = new ZOutputStream(compStream, 1);
            //zNewStream.FlushMode = zlibConst.Z_SYNC_FLUSH;

            byte[] retByte = new byte[0];
            try
            {
                CopyStream(inputStream, zOutputStream);

                byte[] currVerr = new byte[15];
                outputStream.Seek(0, SeekOrigin.Begin);
                outputStream.Read(currVerr, 0, currVerr.Length);
                Console.WriteLine("Changing version from {0} to {1}...", GetVersionInt(currVerr), version);

                var newVerBuff = DEncoding.GetBytes(string.Format("version {0}", version) + System.Environment.NewLine + "Module 0");
                outputStream.Seek(0, SeekOrigin.Begin);
                outputStream.Write(newVerBuff, 0, newVerBuff.Length);
                var fillZero = new byte[outputStream.Length - (outputStream.Position + 1)];
                outputStream.Write(fillZero, 0, fillZero.Length);

                CopyStream(outputStream, zCompStream); 
                zCompStream.finish();
                
                retByte = compStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error changing version: " + ex.ToString());
            }
            finally
            {
                inputStream.Close();
                outputStream.Close();
                zOutputStream.Close();

                compStream.Close();
                zCompStream.Close();
            }

            return retByte;
        }

        private static void Intro()
        {
            Console.Title = "Dragon Nest Client Version Changer v1.01";
            Console.WriteLine(@"
                                                         ..                      
                                                    ..:-=====-::.                
                                                 .:::.         .:::.             
                                              .:-: :=*.  **+  :*=: :-:..         
                                            .:=. --:=-  *+++*  -=--- .=:.        
                                           .-:  =-:-+  +*   *=  +-:-=  --.       
                                          :=.  +---.+  #:   -#  +.-:-+  :=:      
                                         .=.  +==. +*  ++   *+  *+ :==+  :-.     
                                        .-=  =++  -=+-  =#*#-  -++- .++-  =-.    
                                        :=   **:  +==::  :#.  .-===  :**   =:    
                                       .--   *#   ++- **  :  ** -++   #*   =:.   
                                       .--   **   =*  ***. :***  *=   #*   =-    
                                       .:+   -#    +     *+*     +    #:   +:.   
                                        :=               .#.               =:    
                                        .-:       -=-     +     -=-       --.    
                                         .-=   =+-::-+*       *+-::-+-   =-.     
                                          .:= --. ..-**       **::...-- =:.      
                                           .....  .-             -.  .....       
                                                   :=           =:               
                                                   .:==       =-..               
                                                     ..-+  .+-..                 
                                                        .:.:..                   
                                             
                                      Dragon Nest Client Version Changer v1.01
                                                   By: GaestraIDR
                
            ");
        }
    }
}
