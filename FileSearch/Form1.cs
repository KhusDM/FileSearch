using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace FileSearch
{
    enum SearchState
    {
        Begin,
        Stop,
        Continue,
        Done
    };

    public partial class Form1 : Form
    {
        private SearchState searchState;
        private string searchPattern = "";
        private int processedFiles = 0;
        private CancellationTokenSource cts;
        private CancellationToken token;
        private ManualResetEventSlim limiter;
        private Stopwatch stopwatch;
        private Task searchTask;

        private List<string> FilesFound { get; set; }

        public Form1()
        {
            InitializeComponent();

            FilesFound = new List<string>();
            searchState = SearchState.Begin;
            limiter = new ManualResetEventSlim(true);
            stopwatch = new Stopwatch();
            timer1.Enabled = true;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            switch (searchState)
            {
                case SearchState.Begin:
                    searchPattern = "*" + textBox2.Text + "*";
                    cts = new CancellationTokenSource();
                    token = cts.Token;
                    stopwatch.Restart();
                    DirSearchAsync(textBox1.Text, token);
                    button1.Text = "Остановить поиск";
                    button2.Text = "Начать новый поиск";
                    button2.Enabled = true;
                    searchState = SearchState.Stop;
                    break;
                case SearchState.Stop:
                    limiter.Reset();
                    stopwatch.Stop();
                    button1.Text = "Продолжить поиск";
                    searchState = SearchState.Continue;
                    break;
                case SearchState.Continue:
                    limiter.Set();
                    stopwatch.Start();
                    button1.Text = "Остановить поиск";
                    searchState = SearchState.Stop;
                    break;
                case SearchState.Done:
                    stopwatch.Stop();
                    button1.Text = "";
                    button1.Enabled = false;
                    searchState = SearchState.Begin;
                    break;
                default: break;
            }
        }

        //private void DirSearch(string startDirectory, CancellationToken token)
        //{
        //    try
        //    {
        //        foreach (string directory in Directory.GetDirectories(startDirectory))
        //        {
        //            foreach (string file in Directory.GetFiles(directory, searchPattern))
        //            {
        //                string fileText = "";
        //                using (FileStream fileStream = new FileStream(file, FileMode.Open))
        //                {
        //                    label4.Text = file;
        //                    using (StreamReader streamReader = new StreamReader(fileStream))
        //                    {
        //                        if (token.IsCancellationRequested) return;
        //                        limiter.Wait();

        //                        fileText = streamReader.ReadToEnd();
        //                    }
        //                }

        //                if (fileText.Contains(richTextBox1.Text))
        //                    FilesFound.Add(file);

        //                processedFiles++;
        //                label5.Text = processedFiles.ToString();
        //            }

        //            DirSearch(directory, token);
        //        }
        //    }
        //    catch (System.Exception excpt)
        //    {
        //        Console.WriteLine(excpt.Message);
        //    }
        //}

        private void DirSearch(string startDirectory, CancellationToken token)
        {
            try
            {
                foreach (string file in Directory.GetFiles(startDirectory, searchPattern))
                {
                    string fileText = "";
                    using (FileStream fileStream = new FileStream(file, FileMode.Open))
                    {
                        label4.Text = file;
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            if (token.IsCancellationRequested) return;
                            limiter.Wait();

                            fileText = streamReader.ReadToEnd();
                        }
                    }

                    if (fileText.Contains(richTextBox1.Text))
                        FilesFound.Add(file);

                    processedFiles++;
                    label5.Text = processedFiles.ToString();
                }

                foreach (string directory in Directory.GetDirectories(startDirectory))
                {
                    DirSearch(directory, token);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        private async void DirSearchAsync(string startDirectory, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            await Task.Run(() => DirSearch(startDirectory, token));
            searchState = SearchState.Done;
            Button1_Click(new object(), new EventArgs());
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            cts.Cancel();
            if (!limiter.IsSet)
                limiter.Set();

            FilesFound.Clear();
            processedFiles = 0;
            label4.Text = "";
            button1.Enabled = true;
            searchState = SearchState.Begin;

            Button1_Click(sender, e);
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan timeSpan = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
              timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds,
              timeSpan.Milliseconds / 10);

            label6.Text = elapsedTime;
        }
    }
}
