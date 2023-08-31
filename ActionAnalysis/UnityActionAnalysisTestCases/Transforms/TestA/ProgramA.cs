using System;

namespace UnityActionAnalysisTestCases.Transforms.TestA
{
    public class ProgramA
    {
        public void f1()
        {
            try
            {
                if (GetHashCode() > 0)
                {
                    throw new Exception("A");
                } else
                {
                    try
                    {
                        Console.WriteLine("1");
                    } catch (Exception)
                    {
                        Console.WriteLine("2");
                    }
                }
            } catch (Exception)
            {
                Console.WriteLine("Z");
            }
        }

        public void f1_notry()
        {
            if (GetHashCode() > 0) {
                throw new Exception("A");
            } else {
                Console.WriteLine("1");
            }
        }

        public void f2()
        {
            Exception ex = null;
            try
            {
                Console.WriteLine("A");
                try
                {
                    f1();
                    Console.WriteLine("D");
                } catch (Exception)
                {
                    Console.WriteLine("C");
                } finally
                {
                    Console.WriteLine("X");
                    Console.WriteLine(ex);                   
                }
            } catch (Exception e)
            {
                ex = e;
                Console.WriteLine("B");
            }
            Console.WriteLine("E");
            try
            {
                Console.WriteLine("F");
                try
                {
                    Console.WriteLine("G");
                } catch (Exception)
                {
                    Console.WriteLine("H");
                }
            } catch (Exception)
            {
                Console.WriteLine("I");
            }
        }

        public void f2_notry()
        {
            Exception ex = null;
            Console.WriteLine("A");
            f1();
            Console.WriteLine("D");
            Console.WriteLine("X");
            Console.WriteLine(ex);
            Console.WriteLine("E");
            Console.WriteLine("F");
            Console.WriteLine("G");
        }


        public int f3() {
            try {
                Console.WriteLine("A");
                return 2;
            } 
            catch (Exception) {
                Console.WriteLine("B");
                return -1;
            } finally {
                try {
                    Console.WriteLine("C");
                } finally {
                    Console.WriteLine("D");
                }
            }
        }

        public int f3_notry() {
            Console.WriteLine("A");
            int result = 2;
            Console.WriteLine("C");
            Console.WriteLine("D");
            return result;
        }

        public void f4() {
            System.IO.StreamReader sr;
            try
            {
                sr = System.IO.File.OpenText("x");
            } catch (Exception) {
                sr = System.IO.File.OpenText("y");
            }
            while (!sr.EndOfStream)
            {
                try
                {
                    Console.WriteLine(sr.ReadLine());
                } catch (Exception)
                {
                    Console.WriteLine("error");
                }
            }
            Console.WriteLine("done");
        }

        public void f4_notry() {
            System.IO.StreamReader sr;
            sr = System.IO.File.OpenText("x");
            while (!sr.EndOfStream)
            {
                Console.WriteLine(sr.ReadLine());
            }
            Console.WriteLine("done");
        }
    }
}