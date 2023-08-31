using System;
using System.Collections.Generic;
using System.Text;

namespace UnityActionAnalysisTestCases.Symex.TestL
{
    public class ProgramL
    {

        public static void Main(int x, int y)
        {
            try
            {
                if (x == 2) {
                    Console.WriteLine("A");
                }
            } catch (Exception)
            {
                if (x == 1) {
                    Console.WriteLine("B");
                }
            }
            if (x == 3) {
                Console.WriteLine("C");
            } 
            switch (y) {
                case 44:
                case 77:
                    try {
                        if (x + y == 111) {
                            Console.WriteLine("D");
                        }
                    } catch (Exception) {
                    }
                    break;
            }
        }

    }
}
