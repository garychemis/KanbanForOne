using System.Windows;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanForOne
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            // 基础服务
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<AttachmentStorageService>();
            services.AddSingleton<BackupService>();
            services.AddSingleton<WorkHourOptionsService>();
            services.AddSingleton<WorkHourExportService>();

            // 仓储
            services.AddSingleton<TaskRepository>();
            services.AddSingleton<NoteRepository>();
            services.AddSingleton<AttachmentRepository>();
            services.AddSingleton<ArchiveSectionRepository>();
            services.AddSingleton<WorkHourRepository>();

            // 共享状态
            services.AddSingleton<NotificationService>();
            services.AddSingleton<WorkspaceFilterState>();

            // 页面 ViewModel
            services.AddSingleton<WorkHourOptionsViewModel>();
            services.AddSingleton<BoardViewModel>();
            services.AddSingleton<CalendarViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton(sp => new WorkHourSummaryViewModel(
                sp.GetRequiredService<WorkHourRepository>(),
                sp.GetRequiredService<WorkHourExportService>(),
                message => sp.GetRequiredService<NotificationService>().Notify(message)));
            services.AddSingleton<MainWindowViewModel>();

            Services = services.BuildServiceProvider();
            return Services;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            BuildServiceProvider();
            base.OnStartup(e);
        }
    }
}
