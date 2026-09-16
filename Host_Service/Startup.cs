using Host.Azure.Service.Signal;
using Host.Service;
using Host.Service.Azure.Scaling;
using Host.Service.Azure.VM;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Host
{
	public class Startup
	{
		private readonly IConfiguration _config;

		public Startup( IConfiguration tConfig )
		{
			_config = tConfig;
		}

		public void ConfigureServices( IServiceCollection tServices )
		{
			tServices.Configure<Settings>( _config );
			tServices.AddApplicationInsightsTelemetry();
			tServices.AddSingleton<Signal>();
			tServices.AddSingleton<Scaling>();
			tServices.AddSingleton<Service.Azure.FileShare.FileShare>();
			tServices.AddSingleton<VM>();
			tServices.AddHttpClient<Manager>();
			tServices.AddControllers();
			tServices.AddSingleton<Manager>();
			tServices.AddSingleton<IHostedService, Manager>( _ => _.GetService<Manager>() );
		}

		public void Configure( IApplicationBuilder tApp, IWebHostEnvironment tEnv )
		{
			if ( tEnv.IsDevelopment() )
			{
				tApp.UseDeveloperExceptionPage();
			}

			tApp.UseRouting();
			tApp.UseAuthorization();
			tApp.UseEndpoints
			(
				tEndpoints =>
				{
					tEndpoints.MapControllers();
				}
			);
		}
	}
}
