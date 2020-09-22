using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace ConcurrentLab2
{
    class Program
    {
        static bool[] numbers; //true - primary, false - not primary
        static List<int> dividers;
        static int[] currentDividerIndex;
        struct Payload {
            public int numbersFrom;
            public int numbersTo;
            public int dividersFrom;
            public int dividersTo;
            public int eventNum;
            public Payload(int numbersFrom, int numbersTo, int dividersFrom, int dividersTo, int eventNum=0)
            {
                this.numbersFrom = numbersFrom;
                this.numbersTo = numbersTo;
                this.dividersFrom = dividersFrom;
                this.dividersTo = dividersTo;
                this.eventNum = eventNum;
            }
        }

        /// <summary>
        /// Проверяет диапазон чисел от начала до конца на предмет делимости на делители от начала до конца
        /// </summary>
        static void CheckPrimary(object o)
        {
            var payload = (Payload)o;
            for (int i = payload.numbersFrom; i < payload.numbersTo; i++)
            {
                for (int d = payload.dividersFrom; d < payload.dividersTo; d++)
                {
                    if (dividers[d] > i)
                    {
                        break;
                    }
                    if (i != dividers[d] && i % dividers[d] == 0)
                    {
                        numbers[i] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Создает и заполняет массивы чисел и делителей
        /// </summary>
        static void InitArrays(int n)
        {
            numbers = new bool[n + 1];
            Array.Fill<bool>(numbers, true);
            dividers = new List<int>();
        }

        /// <summary>
        /// Применение решета Эратосфена к числам до корня из длины массива чисел
        /// </summary>
        static void FillDividers(int stop)
        {
            for (int i = 2; i < stop; i++)
            {
                if (numbers[i])
                {
                    dividers.Add(i);
                    CheckPrimary(new Payload(i+1, stop, dividers.Count-1, dividers.Count));
                }
            }
        }

        static int ParallelByRange(int threadsCount, int startFrom)
        {
            var threads = new Thread[threadsCount];
            for (int t = 0; t < threadsCount; t++)
            {
                var p = new Payload();
                p.numbersFrom = startFrom + (t * (numbers.Length - startFrom)) / threadsCount; 
                p.numbersTo = startFrom + ((t + 1) * (numbers.Length - startFrom)) / threadsCount;
                p.dividersFrom = 0;
                p.dividersTo = dividers.Count;
                threads[t] = new Thread(CheckPrimary);
                threads[t].Start(p);
            }
            foreach(var thread in threads)
            {
                thread.Join();
            }
            return 0;
        }

        static int ParallelByDividers(int threadsCount, int startFrom)
        {
            var threads = new Thread[threadsCount];
            for (int t = 0; t < threadsCount; t++)
            {
                var p = new Payload();
                p.numbersFrom = startFrom;
                p.numbersTo = numbers.Length;
                p.dividersFrom = (t * dividers.Count) / threadsCount;
                p.dividersTo = ((t + 1) * dividers.Count) / threadsCount;
                threads[t] = new Thread(CheckPrimary);
                threads[t].Start(p);
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            return 0;
        }

        static int ParallelByPool(int threadsCount, int startFrom)
        {
            ThreadPool.SetMinThreads(threadsCount, 0);
            ThreadPool.SetMaxThreads(threadsCount, 0);
            ManualResetEvent[] events = new ManualResetEvent[dividers.Count];
            for (int d = 0; d < dividers.Count; d++)
            {
                events[d] = new ManualResetEvent(false);
                var p = new Payload();
                p.numbersFrom = startFrom;
                p.numbersTo = numbers.Length;
                p.dividersFrom = d;
                p.dividersTo = d+1;
                p.eventNum = d;
                ThreadPool.QueueUserWorkItem((object o) => { var pp = (Payload)o; CheckPrimary(pp); events[pp.eventNum].Set(); }, p);
            }
            foreach (var e in events)
            {
                e.WaitOne();
            }
            return 0;
        }

        static void __ParallelByOwnTask(object o)
        {
            int startFrom = (int)o;
            int index;
            while (true)
            {
                lock (currentDividerIndex)
                {
                    index = currentDividerIndex[0] + 1;
                    currentDividerIndex[0] = index;
                }
                if (index < dividers.Count)
                {
                    var p = new Payload();
                    p.numbersFrom = startFrom;
                    p.numbersTo = numbers.Length;
                    p.dividersFrom = index;
                    p.dividersTo = index + 1;
                    CheckPrimary(p);
                }
                else
                {
                    break;
                }
            }
        }

        static int ParallelByOwn(int threadCount, int startFrom)
        {
            currentDividerIndex = new int[] {-1};
            var threads = new Thread[threadCount];
            for(int i = 0; i < threadCount; i ++)
            {
                threads[i] = new Thread(__ParallelByOwnTask);
                threads[i].Start(startFrom);
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            return 0;
        }

        static void Main(string[] args)
        {
            var ns = new object[] { 1000, 10000, 100000, 1000000};
            var ms = new object[] { 1, 2, 4, 8, 10 };
            var algorithms = new KeyValuePair<String, Func<int, int, int>>[]
            {
                new KeyValuePair<string, Func<int, int, int>>("ParallelByRange", (Func<int, int, int>)ParallelByRange),
                new KeyValuePair<string, Func<int, int, int>>("ParallelByDividers", (Func<int, int, int>)ParallelByDividers),
                new KeyValuePair<string, Func<int, int, int>>("ParallelByPool", (Func<int, int, int>)ParallelByPool),
                new KeyValuePair<string, Func<int, int, int>>("ParallelByOwn", (Func<int, int, int>)ParallelByOwn)
            };
            String headerRow = "|Len\\Thr |";
            String dataRow = "|{0,8}|";
            for (int i = 0; i < ms.Length; i++)
            {
                headerRow += "{" + i + ",8}|";
                dataRow += "{" + (i+1) + ",8}|";
            }
            foreach (var algorithm in algorithms)
            {
                Console.WriteLine(algorithm.Key);
                
                Console.WriteLine(String.Format(headerRow, ms));

                foreach (int n in ns)
                {
                    int SqrtN = (int)Math.Sqrt(n) + 1;
                    InitArrays(n);
                    FillDividers(SqrtN);
                    var results = new object[ms.Length + 1];
                    results[0] = n;
                    var sw = new Stopwatch();
                    for (int i = 0; i < ms.Length; i++)
                    {
                        sw.Restart();
                        algorithm.Value((int)ms[i], 0);
                        sw.Stop();
                        results[i+1] = (int)sw.Elapsed.TotalMilliseconds;
                    }
                    Console.WriteLine(String.Format(dataRow, results));
                }
                Console.WriteLine();
            }
        }
    }
}
