﻿using System;
using System.Reflection;
using IdentityServerWithAspIdAndEF.Data;
using IdentityServerWithAspIdAndEF.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServerWithAspIdAndEF
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            var migrationsAssembly = typeof(ApplicationDbContext).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<ApplicationDbContext>(options => options
                .UseSqlite(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly))
                //.EnableSensitiveDataLogging()
                );

            services.AddMultiTenancy<ApplicationTenant, string>()
                .AddRequestParsers(parsers =>
                {
                    // To test a domain parser locally, add a similar line 
                    // to your hosts file for each tenant you want to test
                    // For Windows: C:\Windows\System32\drivers\etc\hosts
                    // 127.0.0.1	tenant2.local
                    // tenant1 has been mapped to "localhost".
                    parsers.AddDomainParser();
                })
                .AddEntityFrameworkStore<ApplicationDbContext, ApplicationTenant, string>();

            //IdentityModelEventSource.ShowPII = true;
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc(options =>
                {
                    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddAntiforgery();

            services.Configure<IISOptions>(iis =>
                {
                    iis.AuthenticationDisplayName = "Windows";
                    iis.AutomaticAuthentication = false;
                });

            var builder = services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;
                })
                .AddAspNetIdentity<ApplicationUser>()
                // this adds the config data from DB (clients, resources)
                .AddConfigurationStore<ApplicationDbContext>(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlite(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore<ApplicationDbContext>(options =>
                {
                    options.ConfigureDbContext = b =>
                        b.UseSqlite(connectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                    // options.TokenCleanupInterval = 15; // frequency in seconds to cleanup stale grants. 15 is useful during debugging
                });

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                throw new Exception("need to configure key material");
            }

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to http://localhost:port/signin-google
                    options.ClientId = "copy client ID from Google here";
                    options.ClientSecret = "copy client secret from Google here";
                //})
                //.AddOpenIdConnect("oidc", "OpenID Connect", options =>
                //{
                //    options.Authority = "https://demo.identityserver.io/";
                //    options.ClientId = "implicit";
                //    options.SaveTokens = true;

                //    options.TokenValidationParameters = new TokenValidationParameters
                //    {
                //        NameClaimType = "name",
                //        RoleClaimType = "role"
                //    };
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseMultiTenancy<ApplicationTenant>();

            app.UseIdentityServer();

            app.UseAuthentication();

            app.UseMvcWithDefaultRoute();
        }
    }
}
