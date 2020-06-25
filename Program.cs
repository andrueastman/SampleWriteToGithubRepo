using Octokit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GithubTestAppIrvine
{
    class Program
    {
        static async Task Main()
        {
            string token = await GetGithubAppToken();

            // Pass the JWT as a Bearer token to Octokit.net
            var finalClient = new GitHubClient(new ProductHeaderValue("PermissionsScraper"))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            // Get Repo references
            var references = await finalClient.Git.Reference.GetAll("microsoftgraph", "microsoft-graph-devx-content");

            // Check if the test branch is in the refs 
            var testBranch = references.Where(reference => reference.Ref == "refs/heads/test").FirstOrDefault();

            // check if branch already exists.
            if (testBranch == null)
            {
                // test branch does not exist so branch odd dev
                var devBranch = references.Where(reference => reference.Ref == "refs/heads/dev").FirstOrDefault();

                // exception will throw if branch already exists
                var newBranch = await finalClient.Git.Reference.Create("microsoftgraph", "microsoft-graph-devx-content",
                    new NewReference("refs/heads/test", devBranch.Object.Sha));

                // create file
                var createChangeSet = await finalClient.Repository.Content.CreateFile(
                    "microsoftgraph",
                    "microsoft-graph-devx-content",
                    "path/file.txt",
                    new CreateFileRequest("File creation",
                        "Hello Andrew!",
                        "test"));
            }
            else
            {
                // Get reference of test branch
                var masterReference = await finalClient.Git.Reference.Get("microsoftgraph",
                    "microsoft-graph-devx-content", testBranch.Ref);
                // Get the laster commit of this branch
                var latestCommit = await finalClient.Git.Commit.Get("microsoftgraph", "microsoft-graph-devx-content",
                    masterReference.Object.Sha);

                // Create text blob
                var textBlob = new NewBlob {Encoding = EncodingType.Utf8, Content = "Hello Sunday"};
                var textBlobRef =
                    await finalClient.Git.Blob.Create("microsoftgraph", "microsoft-graph-devx-content", textBlob);

                // Create new Tree
                var nt = new NewTree {BaseTree = latestCommit.Tree.Sha};
                // Add items based on blobs
                nt.Tree.Add(new NewTreeItem
                    {Path = "path/file.txt", Mode = "100644", Type = TreeType.Blob, Sha = textBlobRef.Sha});

                var newTree = await finalClient.Git.Tree.Create("microsoftgraph", "microsoft-graph-devx-content", nt);

                // Create Commit
                var newCommit = new NewCommit("Commit test with several files", newTree.Sha,
                    masterReference.Object.Sha);
                var commit =
                    await finalClient.Git.Commit.Create("microsoftgraph", "microsoft-graph-devx-content", newCommit);

                // push the commit
                await finalClient.Git.Reference.Update("microsoftgraph", "microsoft-graph-devx-content", testBranch.Ref,
                    new ReferenceUpdate(commit.Sha));


            }

            // create PR
            var pullRequest = await finalClient.Repository.PullRequest.Create("microsoftgraph",
                "microsoft-graph-devx-content",
                new NewPullRequest("Timely content update", "test", "dev") {Body = "This is a test PR"});

            // Add reviewers
            var teamMembers = new List<string> {"andrueastman", "bettirosengugi"};
            var reviewersResult = await finalClient.Repository.PullRequest.ReviewRequest.Create("microsoftgraph",
                "microsoft-graph-devx-content", pullRequest.Number,
                new PullRequestReviewRequest(teamMembers.AsReadOnly(), null));

            // Add label
            var issueUpdate = new IssueUpdate();
            issueUpdate.AddAssignee("irvinesunday");
            issueUpdate.AddLabel("Generated");

            // Update the PR with the relevant info
            await finalClient.Issue.Update("microsoftgraph", "microsoft-graph-devx-content", pullRequest.Number,
                issueUpdate);
        }

        static async Task<string> GetGithubAppToken()
        {
            // Use GitHubJwt library to create the GitHubApp Jwt Token using our private certificate PEM file
            var generator = new GitHubJwt.GitHubJwtFactory(
                new GitHubJwt.FilePrivateKeySource("cert.pem"),
                new GitHubJwt.GitHubJwtFactoryOptions
                {
                    AppIntegrationId = 70269, // The GitHub App Id
                    ExpirationSeconds = 600 // 10 minutes is the maximum time allowed
                }
            );

            var jwtToken = generator.CreateEncodedJwtToken();

            // Pass the JWT as a Bearer token to Octokit.net
            var appClient = new GitHubClient(new ProductHeaderValue("PermissionsScraper"))
            {
                Credentials = new Credentials(jwtToken, AuthenticationType.Bearer)
            };

            // Get a list of installations for the authenticated GitHubApp and installationID for microsoftgraph
            var installations = await appClient.GitHubApps.GetAllInstallationsForCurrent();
            var id = installations.Where(installation => installation.Account.Login == "microsoftgraph")
                .FirstOrDefault().Id;

            // Create an Installation token for the microsoftgraph installation instance
            var response = await appClient.GitHubApps.CreateInstallationToken(id);

            string token = response.Token;

            return token;
        }
    }
}
