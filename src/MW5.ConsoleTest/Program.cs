﻿using System;

namespace MW5.ConsoleTest
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Tests.TestCallback();
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception occured: " + ex.Message);
            }
            Console.ReadLine();
        }
    }
}