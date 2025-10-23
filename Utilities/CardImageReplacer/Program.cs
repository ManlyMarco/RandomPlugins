using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CardImageReplacer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var isSilent = args.Length >= 3;

            string cardPath;
            if (args.Length < 1)
            {
                cardPath = AskForCard();
                if (string.IsNullOrEmpty(cardPath)) return;
            }
            else
            {
                cardPath = args[0];
                if (string.IsNullOrEmpty(cardPath))
                {
                    Console.WriteLine("Invalid arguments");
                    return;
                }
            }

            string replacementPath;
            if (args.Length < 2)
            {
                replacementPath = AskForReplacementImage();
                if (string.IsNullOrEmpty(replacementPath)) return;
            }
            else
            {
                replacementPath = args[1];
                if (string.IsNullOrEmpty(replacementPath))
                {
                    Console.WriteLine("Invalid arguments");
                    return;
                }
            }

            string savePath;
            if (args.Length < 3)
            {
                savePath = AskToSave();
                if (string.IsNullOrEmpty(savePath)) return;
            }
            else
            {
                savePath = args[2];
                if (string.IsNullOrEmpty(savePath))
                {
                    Console.WriteLine("Invalid arguments, pass full paths to card, replacement image, and save location for the modified card");
                    return;
                }
            }

            IEnumerable<byte> trimmedCard;
            Image origImage;
            try
            {
                var card = File.ReadAllBytes(cardPath);
                var cardImageEnd = FindImageEnd(card);
                trimmedCard = card.Skip(cardImageEnd);

                origImage = Image.FromStream(new MemoryStream(card.Take(cardImageEnd).ToArray()));
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to parse card - " + e);
                if (!isSilent)
                    MessageBox.Show("Failed to parse card - " + e.Message);
                return;
            }

            IEnumerable<byte> replacementImage;
            Image replaceImage;
            try
            {
                var replacement = File.ReadAllBytes(replacementPath);
                var replacementImageEnd = FindImageEnd(replacement);
                replacementImage = replacement.Take(replacementImageEnd);

                replaceImage = Image.FromStream(new MemoryStream(replacement.Take(replacementImageEnd).ToArray()));
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to parse replacement image - " + e);
                if (!isSilent)
                    MessageBox.Show("Failed to parse replacement image - " + e.Message);
                return;
            }

            if (replaceImage.Width != origImage.Width || replaceImage.Height != origImage.Height)
            {
                Console.WriteLine(
                    $"Replacement image has different size than the original image, you might experience issues!\n" +
                    $"Original: {origImage.Width}x{origImage.Height} Replacement: {replaceImage.Width}x{replaceImage.Height}");
                if (!isSilent)
                {
                    MessageBox.Show(
                        $"Replacement image has different size than the original image, you might experience issues!\n" +
                        $"Original: {origImage.Width}x{origImage.Height} Replacement: {replaceImage.Width}x{replaceImage.Height}");
                }
            }

            try
            {
                var result = replacementImage.Concat(trimmedCard).ToArray();
                File.WriteAllBytes(savePath, result);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to save edited card - " + e);
                if (!isSilent)
                    MessageBox.Show("Failed to save edited card - " + e.Message);
            }
        }

        private static string AskForCard()
        {
            using (var fd = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "PNG files|*.png",
                Title = "Select character card"
            })
            {
                if (fd.ShowDialog() == DialogResult.OK)
                    return fd.FileName;
            }

            return null;
        }

        private static string AskForReplacementImage()
        {
            using (var fd = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "PNG files|*.png",
                Title = "Select a new card image"
            })
            {
                if (fd.ShowDialog() == DialogResult.OK)
                    return fd.FileName;
            }

            return null;
        }

        private static string AskToSave()
        {
            using (var fd = new SaveFileDialog
            {
                CheckPathExists = true,
                ValidateNames = true,
                Filter = "PNG files|*.png",
                Title = "Save modified card",
                DefaultExt = ".png",
                FileName = "modified card"
            })
            {
                if (fd.ShowDialog() == DialogResult.OK)
                    return fd.FileName;
            }

            return null;
        }

        private static int FindImageEnd(IList<byte> bytes)
        {
            byte[] IENDChunk = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

            for (var index = 0; index < bytes.Count - IENDChunk.Length + 1; index++)
            {
                var hit = true;

                for (var x = 0; x < IENDChunk.Length; x++)
                {
                    if (IENDChunk[x] != bytes[index + x])
                    {
                        hit = false;
                        break;
                    }
                }

                if (hit)
                    return index + IENDChunk.Length;
            }
            throw new IOException("No image found in file");
        }
    }
}
