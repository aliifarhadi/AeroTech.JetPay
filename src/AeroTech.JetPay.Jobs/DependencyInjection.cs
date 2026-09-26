using AeroTech.JetPay.Jobs.PaymentSessionAggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace AeroTech.JetPay.Jobs
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
        {
            var expirySection = configuration.GetSection(PaymentExpiryOptions.SectionName);
            var expiry = expirySection.Get<PaymentExpiryOptions>() ?? new PaymentExpiryOptions();

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                expiry.IntervalSeconds,
                $"{PaymentExpiryOptions.SectionName}:{nameof(PaymentExpiryOptions.IntervalSeconds)}");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                expiry.MaxBatch,
                $"{PaymentExpiryOptions.SectionName}:{nameof(PaymentExpiryOptions.MaxBatch)}");

            services.Configure<PaymentExpiryOptions>(expirySection);

            services.AddQuartz(quartz =>
            {
                var expiryKey = new JobKey(nameof(ExpireDuePaymentsJob));
                quartz.AddJob<ExpireDuePaymentsJob>(expiryKey);
                quartz.AddTrigger(trigger => trigger
                    .ForJob(expiryKey)
                    .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(expiry.IntervalSeconds).RepeatForever()));
            });
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
            return services;
        }
    }
}
