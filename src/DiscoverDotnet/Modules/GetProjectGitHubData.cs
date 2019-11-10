﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Octokit;
using Statiq.Common;

namespace DiscoverDotnet.Modules
{
    public class GetProjectGitHubData : Module
    {
        private GitHubManager _gitHub;
        private FoundationManager _foundation;

        protected override async Task BeforeExecutionAsync(IExecutionContext context)
        {
            if (_gitHub == null)
            {
                _gitHub = context.GetRequiredService<GitHubManager>();
            }
            if (_foundation == null)
            {
                _foundation = context.GetRequiredService<FoundationManager>();
                await _foundation.PopulateAsync(context);
            }
        }

        protected override async Task<IEnumerable<IDocument>> ExecuteInputAsync(IDocument input, IExecutionContext context)
        {
            IDocument output = input;

            // Extract the GitHub owner and name
            if (Uri.TryCreate(input.GetString("Source"), UriKind.Absolute, out Uri source)
                && source.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
            {
                string owner = source.Segments[1].Trim('/');
                string name = source.Segments[2].Trim('/');

                // Get the repository
                context.LogInformation($"Getting GitHub project data for {owner}/{name}");
                Repository repository = await _gitHub.GetAsync(x => x.Repository.Get(owner, name), context);

                // Get the metadata
                MetadataItems metadata = new MetadataItems
                    {
                        { "StargazersCount", repository.StargazersCount },
                        { "ForksCount", repository.ForksCount },
                        { "OpenIssuesCount", repository.OpenIssuesCount },
                        { "PushedAt", repository.PushedAt },
                        { "CreatedAt", repository.CreatedAt }
                    };
                if (!input.ContainsKey("Title"))
                {
                    metadata.Add("Title", repository.Name);
                }
                if (!input.ContainsKey("Description"))
                {
                    metadata.Add("Description", repository.Description);
                }
                if (!input.ContainsKey("Website"))
                {
                    metadata.Add("Website", repository.Homepage);
                }
                if (!input.ContainsKey("Microsoft") && GitHubManager.MicrosoftOwners.Contains(owner))
                {
                    metadata.Add("Microsoft", true);
                }
                if (!input.ContainsKey("Foundation") && _foundation.IsInFoundation(owner, name))
                {
                    metadata.Add("Foundation", true);
                }

                // Get the readme (will throw if there's no readme)
                context.LogInformation($"Getting GitHub readme for {owner}/{name}");
                try
                {
                    string readme = await _gitHub.GetAsync(x => x.Repository.Content.GetReadmeHtml(owner, name), context, false);
                    if (!string.IsNullOrEmpty(readme))
                    {
                        metadata.Add("Readme", readme);
                    }
                }
                catch (Exception ex)
                {
                    context.LogInformation($"Exception getting GitHub readme for {owner}/{name}: {ex.Message}");
                }

                output = input.Clone(metadata);
            }

            return output.Yield();
        }
    }
}
