using Microsoft.Extensions.DependencyInjection;
using WRADI.Database.PostgreSQL.WrInspectionReport.Services;
using WRADI.DocumentType.WrInspectionReport.Interfaces;

namespace WRADI.Database.PostgreSQL.WrInspectionReport;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWrInspectionReportPostgreSqlServices(
        this IServiceCollection services)
    {
        services.AddTransient<IWrInspectionReportSummaryReadService, PostgresWrInspectionReportSummaryReadService>();
        services.AddTransient<IWrInspectionReportSummaryWriteService, PostgresWrInspectionReportSummaryWriteService>();

        return services;
    }
}
