using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VIEWORKS_AFTS
{
    static class Program
    {
        /// <summary>
        /// 해당 응용 프로그램의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localDllDir = System.IO.Path.Combine(baseDir, "DLL");

            // 1. 네이티브 DLL(OpenCV, SkiaSharp 등) 경로 주입 (실행 폴더 및 DLL 하위 폴더)
            Environment.SetEnvironmentVariable("PATH", baseDir + ";" + localDllDir + ";" + Environment.GetEnvironmentVariable("PATH"));

            // 2. 관리형 DLL(C#) 동적 리졸버 (실행 디렉토리 및 DLL 폴더 탐색)
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var simpleName = new System.Reflection.AssemblyName(args.Name).Name;
                var candidate1 = System.IO.Path.Combine(baseDir, simpleName + ".dll");
                if (System.IO.File.Exists(candidate1)) return System.Reflection.Assembly.LoadFrom(candidate1);
                var candidate2 = System.IO.Path.Combine(localDllDir, simpleName + ".dll");
                if (System.IO.File.Exists(candidate2)) return System.Reflection.Assembly.LoadFrom(candidate2);
                return null;
            };
            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.ToString(), "Thread Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                MessageBox.Show(e.ExceptionObject?.ToString(), "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);

            RunApplication();
        }

        // JIT Eager Loading으로 인한 FileLoadException 방지: Vieworks_AFTS 참조를 Main에서 분리
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RunApplication()
        {
            try
            {
                Environment.SetEnvironmentVariable("OPENCV_IO_MAX_IMAGE_PIXELS", "3000000000");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Vieworks_AFTS());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
