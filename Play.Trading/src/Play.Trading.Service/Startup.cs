using System;
using System.Reflection;
using System.Text.Json.Serialization;
using GreenPipes;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Play.Common.Identity;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Common.Settings;
using Play.Inventory.Contracts;
using Play.Trading.Service.Entities;
using Play.Trading.Service.Exceptions;
using Play.Trading.Service.Settings;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service
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
            services.AddMongo()
                    .AddMongoRepository<CatalogItem>("catalogitems")
                    .AddJwtBearerAuthentication();
            AddMassTransit(services);

            services.AddControllers(options =>
            {
                options.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Trading.Service", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void AddMassTransit(IServiceCollection services)
        {
            services.AddMassTransit(configure =>
            {
                configure.UsingPlayEconomyRabbitMq(retryConfigurator =>
                {
                    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                    retryConfigurator.Ignore(typeof(UnknownItemException));
                });
                configure.AddConsumers(Assembly.GetEntryAssembly());
                configure.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(sagaConfigurator =>
                {
                    sagaConfigurator.UseInMemoryOutbox();
                })
                    .MongoDbRepository(r =>
                    {
                        var serviceSettings = Configuration.GetSection(nameof(ServiceSettings))
                                                           .Get<ServiceSettings>();
                        var mongoSettings = Configuration.GetSection(nameof(MongoDbSettings))
                                                           .Get<MongoDbSettings>();

                        r.Connection = mongoSettings.ConnectionString;
                        r.DatabaseName = serviceSettings.ServiceName;
                    });
            });

            var queueSettings = Configuration.GetSection(nameof(QueueSettings))
                                                           .Get<QueueSettings>();

            EndpointConvention.Map<GrantItems>(new Uri(queueSettings.GrantItemsQueueAddress));

            services.AddMassTransitHostedService();
            services.AddGenericRequestClient();
        }
    }
}
