using System;
using System.Net;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MediatR;
using Serilog;
using SuperSafeBank.Core;
using SuperSafeBank.Domain;
using SuperSafeBank.Domain.Commands;
using SuperSafeBank.Domain.Events;
using SuperSafeBank.Domain.Services;
using SuperSafeBank.Web.API.EventHandlers;
using SuperSafeBank.Web.API.Registries;

namespace SuperSafeBank.Web.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddSingleton<ICurrencyConverter, FakeCurrencyConverter>();

            services.AddSingleton<IEventSerializer>(new JsonEventSerializer(new[]
            {
                typeof(CustomerCreated).Assembly
            })).AddInfrastructure(this.Configuration);

            services.AddScoped<ServiceFactory>(ctx => ctx.GetRequiredService);
            services.AddScoped<IMediator, Mediator>();

            services.Scan(scan =>
            {
                scan.FromAssembliesOf(typeof(CreateCustomer))
                    .RegisterHandlers(typeof(IRequestHandler<>))
                    .RegisterHandlers(typeof(IRequestHandler<,>))
                    .RegisterHandlers(typeof(INotificationHandler<>));
            });

            services.Decorate(typeof(INotificationHandler<>), typeof(RetryDecorator<>));

            services.AddProblemDetails(opts =>
            {
                opts.IncludeExceptionDetails = (ctx, ex) =>
                {
                    var env = ctx.RequestServices.GetRequiredService<IHostEnvironment>();
                    return env.IsDevelopment() || env.IsStaging();
                };

                opts.MapToStatusCode<ArgumentOutOfRangeException>((int) HttpStatusCode.BadRequest);
                opts.MapToStatusCode<ValidationException>((int)HttpStatusCode.BadRequest);
                opts.MapToStatusCode<AccountTransactionException>((int)HttpStatusCode.BadRequest);
            });

#if OnPremise
            services.RegisterWorker(this.Configuration);
#endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSerilogRequestLogging();

            app.UseProblemDetails();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }

    }
}
