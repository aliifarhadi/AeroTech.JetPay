using AeroTech.JetPay.Jobs.PaymentIntentAggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace AeroTech.JetPay.Jobs
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
        {
            var expirySection = configuration.GetSection(PaymentIntentExpiryOptions.SectionName);
            var expiry = expirySection.Get<PaymentIntentExpiryOptions>() ?? new PaymentIntentExpiryOptions();

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                expiry.IntervalSeconds,
                $"{PaymentIntentExpiryOptions.SectionName}:{nameof(PaymentIntentExpiryOptions.IntervalSeconds)}");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                expiry.MaxBatch,
                $"{PaymentIntentExpiryOptions.SectionName}:{nameof(PaymentIntentExpiryOptions.MaxBatch)}");

            services.Configure<PaymentIntentExpiryOptions>(expirySection);

            services.AddQuartz(quartz =>
            {
                var expiryKey = new JobKey(nameof(ExpireDuePaymentIntentsJob));
                quartz.AddJob<ExpireDuePaymentIntentsJob>(expiryKey);
                quartz.AddTrigger(trigger => trigger
                    .ForJob(expiryKey)
                    .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(expiry.IntervalSeconds).RepeatForever()));
            });
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
            return services;
        }
    }
}
