using System.Windows;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;

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
            services.AddSingleton<UnifiedBackupService>();
            services.AddSingleton<WorkHourOptionsService>();
            services.AddSingleton<WorkHourExportService>();

            // 独立设计条件模块
            services.AddSingleton<DesignConditionStorageOptions>();
            services.AddSingleton<DesignConditionOperationCoordinator>();
            services.AddSingleton<DesignConditionDatabaseService>();
            services.AddSingleton<DesignConditionAttachmentStorageService>();
            services.AddSingleton<DesignConditionExportService>();
            services.AddSingleton<DesignConditionBackupService>();

            // 仓储
            services.AddSingleton<TaskRepository>();
            services.AddSingleton<NoteRepository>();
            services.AddSingleton<AttachmentRepository>();
            services.AddSingleton<ArchiveSectionRepository>();
            services.AddSingleton<WorkHourRepository>();
            services.AddSingleton<DesignConditionAttachmentRepository>();
            services.AddSingleton<DesignConditionRepository>();
            services.AddSingleton<DesignConditionOptionRepository>();

            // 共享状态
            services.AddSingleton<NotificationService>();
            services.AddSingleton<WorkspaceFilterState>();

            // 页面 ViewModel
            services.AddSingleton<WorkHourOptionsViewModel>();
            services.AddSingleton<DesignConditionViewModel>();
            services.AddSingleton<DesignConditionCalendarSectionViewModel>();
            services.AddSingleton<BoardViewModel>();
            services.AddSingleton<CalendarViewModel>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<AboutViewModel>();
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
