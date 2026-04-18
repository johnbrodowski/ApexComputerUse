using System.Collections.ObjectModel;
using WpfApplication.Infrastructure;

namespace WpfApplication
{
    public class TortureTestViewModel : ObservableObject
    {
        public ObservableCollection<JobItem>     Jobs     { get; }
        public ObservableCollection<ServiceItem> Services { get; }

        public string SelectedDepartment { get => GetProperty<string>(); set => SetProperty(value); }
        public string SelectedStatus     { get => GetProperty<string>(); set => SetProperty(value); }
        public bool   IsConnected        { get => GetProperty<bool>();   set => SetProperty(value); }
        public double AccessLevel        { get => GetProperty<double>(); set => SetProperty(value); }
        public double UploadLimit        { get => GetProperty<double>(); set => SetProperty(value); }
        public double DownloadLimit      { get => GetProperty<double>(); set => SetProperty(value); }

        public TortureTestViewModel()
        {
            IsConnected        = true;
            AccessLevel        = 3;
            UploadLimit        = 100;
            DownloadLimit      = 500;
            SelectedDepartment = "IT Operations";
            SelectedStatus     = "Active";

            Jobs = new ObservableCollection<JobItem>
            {
                new JobItem { Name = "nightly-backup",  Type = "Backup",       Frequency = "Daily",        LastRun = "2026-03-24 02:00", NextRun = "2026-03-25 02:00", Status = "Success", Duration = "14m 32s", IsEnabled = true  },
                new JobItem { Name = "weekly-report",   Type = "Report",       Frequency = "Weekly",       LastRun = "2026-03-22 06:00", NextRun = "2026-03-29 06:00", Status = "Success", Duration = "2m 11s",  IsEnabled = true  },
                new JobItem { Name = "hourly-sync",     Type = "Sync",         Frequency = "Hourly",       LastRun = "2026-03-25 11:00", NextRun = "2026-03-25 12:00", Status = "Running", Duration = "0m 43s",  IsEnabled = true  },
                new JobItem { Name = "log-cleanup",     Type = "Cleanup",      Frequency = "Daily",        LastRun = "2026-03-24 03:00", NextRun = "2026-03-25 03:00", Status = "Failed",  Duration = "0m 02s",  IsEnabled = false },
                new JobItem { Name = "health-check",    Type = "Health Check", Frequency = "Every Minute", LastRun = "2026-03-25 11:03", NextRun = "2026-03-25 11:04", Status = "Success", Duration = "0m 01s",  IsEnabled = true  },
                new JobItem { Name = "monthly-invoice", Type = "Notification", Frequency = "Monthly",      LastRun = "2026-03-01 08:00", NextRun = "2026-04-01 08:00", Status = "Success", Duration = "0m 48s",  IsEnabled = true  },
            };

            Services = new ObservableCollection<ServiceItem>
            {
                new ServiceItem { ServiceName = "AuthService",      Host = "auth.internal",    Port = 8443, IsEnabled = true,  Environment = "Production", Latency = 12,  LastSeen = "2026-03-25 11:00" },
                new ServiceItem { ServiceName = "UserAPI",          Host = "users.internal",   Port = 8080, IsEnabled = true,  Environment = "Production", Latency = 8,   LastSeen = "2026-03-25 11:01" },
                new ServiceItem { ServiceName = "PaymentGateway",   Host = "pay.internal",     Port = 443,  IsEnabled = true,  Environment = "Production", Latency = 145, LastSeen = "2026-03-25 10:58" },
                new ServiceItem { ServiceName = "NotificationSvc",  Host = "notify.internal",  Port = 5672, IsEnabled = false, Environment = "Staging",    Latency = 22,  LastSeen = "2026-03-24 16:00" },
                new ServiceItem { ServiceName = "ReportingEngine",  Host = "reports.internal", Port = 9090, IsEnabled = true,  Environment = "Dev",        Latency = 55,  LastSeen = "2026-03-25 09:30" },
                new ServiceItem { ServiceName = "CacheService",     Host = "cache.internal",   Port = 6379, IsEnabled = true,  Environment = "Production", Latency = 2,   LastSeen = "2026-03-25 11:02" },
                new ServiceItem { ServiceName = "SearchIndex",      Host = "search.internal",  Port = 9200, IsEnabled = true,  Environment = "Production", Latency = 18,  LastSeen = "2026-03-25 11:01" },
            };
        }
    }

    public class JobItem : ObservableObject
    {
        public string Name      { get => GetProperty<string>(); set => SetProperty(value); }
        public string Type      { get => GetProperty<string>(); set => SetProperty(value); }
        public string Frequency { get => GetProperty<string>(); set => SetProperty(value); }
        public string LastRun   { get => GetProperty<string>(); set => SetProperty(value); }
        public string NextRun   { get => GetProperty<string>(); set => SetProperty(value); }
        public string Status    { get => GetProperty<string>(); set => SetProperty(value); }
        public string Duration  { get => GetProperty<string>(); set => SetProperty(value); }
        public bool   IsEnabled { get => GetProperty<bool>();   set => SetProperty(value); }
    }

    public class ServiceItem : ObservableObject
    {
        public string ServiceName { get => GetProperty<string>(); set => SetProperty(value); }
        public string Host        { get => GetProperty<string>(); set => SetProperty(value); }
        public int    Port        { get => GetProperty<int>();    set => SetProperty(value); }
        public bool   IsEnabled   { get => GetProperty<bool>();   set => SetProperty(value); }
        public string Environment { get => GetProperty<string>(); set => SetProperty(value); }
        public int    Latency     { get => GetProperty<int>();    set => SetProperty(value); }
        public string LastSeen    { get => GetProperty<string>(); set => SetProperty(value); }
    }
}
