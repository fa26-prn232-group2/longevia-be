using Microsoft.EntityFrameworkCore;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Persistence;

public sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options)
    : DbContext(options), IUnitOfWork;
