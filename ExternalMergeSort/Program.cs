using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ExternalMergeSort
{
  class Program
  {
    static void Main(string[] args)
    {
      // This does a external merge sort on a big file
      // http://en.wikipedia.org/wiki/External_sorting
      // The idea is to keep the memory usage below 50megs.

      Split("c:\\BigFile.txt");

      MemoryUsage();

      SortTheChunks();

      MemoryUsage();

      MergeTheChunks();

      MemoryUsage();
    }

    /// <summary>
    /// Merge all the "sorted00058.dat" chunks together 
    /// Uses 45MB of ram, for 100 chunks
    /// Takes 5 minutes, for 100 chunks of 10 megs each ie 1 gig total
    /// </summary>
    static void MergeTheChunks()
    {
      W("Merging");

      string[] paths = Directory.GetFiles("C:\\", "sorted*.dat");
      int chunks = paths.Length; // Number of chunks
      int recordsize = 100; // estimated record size
      int records = 10000000; // estimated total # records
      int maxusage = 500000000; // max memory usage
      int buffersize = maxusage / chunks; // size in bytes of each buffer
      double recordoverhead = 7.5; // The overhead of using Queue<>
      int bufferlen = (int)(buffersize / recordsize / recordoverhead); // number of records in each buffer

      // Open the files
      StreamReader[] readers = new StreamReader[chunks];
      for (int i = 0; i < chunks; i++)
        readers[i] = new StreamReader(paths[i]);

      // Make the queues
      Queue<string>[] queues = new Queue<string>[chunks];
      for (int i = 0; i < chunks; i++)
        queues[i] = new Queue<string>(bufferlen);

      // Load the queues
      W("Priming the queues");
      for (int i = 0; i < chunks; i++)
        LoadQueue(queues[i], readers[i], bufferlen);
      W("Priming the queues complete");

      // Merge!
      StreamWriter sw = new StreamWriter("C:\\BigFileSorted.txt");
      bool done = false;
      int lowest_index, j, progress = 0;
      string lowest_value;
      while (!done)
      {
        // Report the progress
        if (++progress % 5000 == 0)
          Console.Write("{0:f2}%   \r",
            100.0 * progress / records);

        // Find the chunk with the lowest value
        lowest_index = -1;
        lowest_value = "";
        for (j = 0; j < chunks; j++)
        {
          if (queues[j] != null)
          {
            if (lowest_index < 0 || String.CompareOrdinal(queues[j].Peek(), lowest_value) < 0)
            {
              lowest_index = j;
              lowest_value = queues[j].Peek();
            }
          }
        }

        // Was nothing found in any queue? We must be done then.
        if (lowest_index == -1) { done = true; break; }

        // Output it
        sw.WriteLine(lowest_value);

        // Remove from queue
        queues[lowest_index].Dequeue();
        // Have we emptied the queue? Top it up
        if (queues[lowest_index].Count == 0)
        {
          LoadQueue(queues[lowest_index], readers[lowest_index], bufferlen);
          // Was there nothing left to read?
          if (queues[lowest_index].Count == 0)
          {
            queues[lowest_index] = null;
          }
        }
      }
      sw.Close();

      // Close and delete the files
      for (int i = 0; i < chunks; i++)
      {
        readers[i].Close();
        File.Delete(paths[i]);
      }

      W("Merging complete");
    }

    /// <summary>
    /// Loads up to a number of records into a queue
    /// </summary>
    static void LoadQueue(Queue<string> queue, StreamReader file, int records)
    {
      for (int i = 0; i < records; i++)
      {
        if (file.Peek() < 0) break;
        queue.Enqueue(file.ReadLine());
      }
    }

    /// <summary>
    /// Go through all the "split00058.dat" files, and sort them
    /// into "sorted00058.dat" files, removing the original
    /// This should use 37megs of memory, for chunks of 10megs
    /// Takes about 2 minutes.
    /// </summary>
    static void SortTheChunks()
    {
      W("Sorting chunks");
      foreach (string path in Directory.GetFiles("C:\\", "split*.dat"))
      {
        Console.Write("{0}     \r", path);

        // Read all lines into an array
        string[] contents = File.ReadAllLines(path);
        // Sort the in-memory array
        Array.Sort(contents);
        // Create the 'sorted' filename
        string newpath = path.Replace("split", "sorted");
        // Write it
        File.WriteAllLines(newpath, contents);
        // Delete the unsorted chunk
        File.Delete(path);
        // Free the in-memory sorted array
        contents = null;
        GC.Collect();
      }
      W("Sorting chunks completed");
    }

    /// <summary>
    /// Split the big file into chunks
    /// This kept memory usage to 8mb, with 10mb chunks
    /// It took 4 minutes for a 1gig source file
    /// </summary>
    static void Split(string file)
    {
      W("Splitting");
      int split_num = 1;
      StreamWriter sw = new StreamWriter(string.Format("c:\\split{0:d5}.dat", split_num));
      long read_line = 0;
      using (StreamReader sr = new StreamReader(file))
      {
        while (sr.Peek() >= 0)
        {
          // Progress reporting
          if (++read_line % 5000 == 0)
            Console.Write("{0:f2}%   \r",
              100.0 * sr.BaseStream.Position / sr.BaseStream.Length);

          // Copy a line
          sw.WriteLine(sr.ReadLine());

          // If the file is big, then make a new split,
          // however if this was the last line then don't bother
          if (sw.BaseStream.Length > 100000000 && sr.Peek() >= 0)
          {
            sw.Close();
            split_num++;
            sw = new StreamWriter(string.Format("c:\\split{0:d5}.dat", split_num));
          }
        }
      }
      sw.Close();
      W("Splitting complete");
    }

    /// <summary>
    /// Write to console, with the time
    /// </summary>
    static void W(string s)
    {
      Console.WriteLine("{0}: {1}", DateTime.Now.ToLongTimeString(), s);
    }

    /// <summary>
    /// Print memory usage
    /// </summary>
    static void MemoryUsage()
    {
      W(String.Format("{0} MB peak working set | {1} MB private bytes",
        Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024,
        Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024
        ));
    }
  }
}
