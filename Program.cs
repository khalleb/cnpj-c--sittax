using Microsoft.EntityFrameworkCore;

using Sittax.Cnpj.Data;
using Sittax.Cnpj.Repositories;
using Sittax.Cnpj.Services;
using Sittax.Cnpj.Workers;
using Sittax.Cnpj.Workers.ReceitaFederalDados;
using Sittax.Domain.Core;
using Sittax.Domain.Core.Configuration;

using STX.Core;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine(builder.Environment.EnvironmentName);
_ = new AppSettingsBase(builder.Environment.EnvironmentName);

builder.UsePorts("CNPJ_PORTS", "https://+:5077,http://+:5078");
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.AddCors();

// ========== CONFIGURAÇÃO DO BANCO DE DADOS POSTGRES ==========
// Configurar PostgreSQL com Entity Framework

builder.Services.AddDbContext<SittaxCnpjDbContext>(options =>
{
    options.UseNpgsql(AppSettingsBase.ConexaoPostgres, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(300);
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
    });
});

// ========== REGISTRAR REPOSITÓRIOS ==========
builder.Services.AddScoped<IReceitaFederalCnaesRepository, ReceitaFederalCnaesRepository>();
builder.Services.AddScoped<IReceitaFederalEmpresasRepository, ReceitaFederalEmpresasRepository>();
builder.Services.AddScoped<IReceitaFederalEstabelecimentosRepository, ReceitaFederalEstabelecimentosRepository>();
builder.Services.AddScoped<IReceitaFederalLogArquivosRepository, ReceitaFederalLogArquivosRepository>();
builder.Services.AddScoped<IReceitaFederalMotivosRepository, ReceitaFederalMotivosRepository>();
builder.Services.AddScoped<IReceitaFederalMunicipiosRepository, ReceitaFederalMunicipiosRepository>();
builder.Services.AddScoped<IReceitaFederalNaturezasRepository, ReceitaFederalNaturezasRepository>();
builder.Services.AddScoped<IReceitaFederalPaisesRepository, ReceitaFederalPaisesRepository>();
builder.Services.AddScoped<IReceitaFederalQualificacoesRepository, ReceitaFederalQualificacoesRepository>();
builder.Services.AddScoped<IReceitaFederalSimplesRepository, ReceitaFederalSimplesRepository>();
builder.Services.AddScoped<IReceitaFederalSociosRepository, ReceitaFederalSociosRepository>();

builder.Services.AddScoped<ReceitaFederalLogArquivosService>();
builder.Services.AddScoped<ReceitaFederalCsvProcessorService>();


// ========== REGISTRAR PROGRESS MANAGER ==========
builder.Services.AddSingleton<GerenciadorDownloadProgresso>();

// ========== REGISTRAR WORKER ==========
builder.Services.AddHostedService<ReceitaFederalDadosEmpresasWorker>();

var app = builder.Build();
XEnvironment.Services = app.Services;

app.UseHttpsRedirection();
app.AddCultureBr();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("EnableCORS");
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints => { endpoints.MapControllerRoute("default", "{controller}/{action=Index}/{id?}").RequireCors("EnableCORS"); });

app.Run();
