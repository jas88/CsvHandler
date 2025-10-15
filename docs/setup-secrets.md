# GitHub Secrets Setup Guide

This guide walks you through setting up the required secrets for the CI/CD pipeline.

## Required Secrets

The CI/CD pipeline requires two secrets to be configured in your GitHub repository:

1. **CODECOV_TOKEN** - For uploading code coverage reports
2. **NUGET_API_KEY** - For publishing packages to NuGet.org

## 1. Codecov Setup

### Step 1: Create Codecov Account

1. Navigate to https://codecov.io
2. Click "Sign up with GitHub"
3. Authorize Codecov to access your GitHub account

### Step 2: Add Repository

1. Once logged in, click "Add new repository" or navigate to https://codecov.io/gh
2. Find `jas88/CsvHandler` in the list
3. Click "Setup repo"

### Step 3: Get Upload Token

1. You'll be taken to the repository setup page
2. Copy the upload token displayed (starts with a long alphanumeric string)
3. Keep this tab open - you'll need this token

### Step 4: Add GitHub Secret

1. Open a new tab and navigate to your GitHub repository
2. Go to **Settings** > **Secrets and variables** > **Actions**
3. Click **New repository secret**
4. Enter the details:
   - **Name:** `CODECOV_TOKEN`
   - **Secret:** Paste the token from Codecov
5. Click **Add secret**

### Step 5: Update Badge Token

1. Back in Codecov, navigate to Settings > Badge
2. Copy the badge token (different from upload token, used only in URLs)
3. In your README.md, replace `YOUR_CODECOV_TOKEN` with this badge token:

```markdown
[![codecov](https://codecov.io/gh/jas88/CsvHandler/branch/main/graph/badge.svg?token=BADGE_TOKEN_HERE)](https://codecov.io/gh/jas88/CsvHandler)
```

**Note:** The badge token is safe to commit publicly - it only allows viewing coverage, not uploading.

## 2. NuGet.org Setup

### Step 1: Create NuGet Account

1. Navigate to https://www.nuget.org
2. Click "Sign in" (top right)
3. If you don't have an account, click "Register" and create one
4. Verify your email address

### Step 2: Generate API Key

1. Once logged in, click your username (top right) > **API Keys**
2. Click **Create** to generate a new API key
3. Configure the API key:
   - **Key Name:** `GitHub Actions - CsvHandler`
   - **Package Owner:** Select your account
   - **Glob Pattern:** `CsvHandler*` (allows all packages starting with CsvHandler)
   - **Scopes:** Select **Push new packages and package versions**
   - **Expiration:** Choose an expiration date (e.g., 365 days)
4. Click **Create**

### Step 3: Copy API Key

1. **IMPORTANT:** The API key will only be shown once!
2. Copy the entire API key immediately
3. Store it temporarily in a secure location (password manager)

### Step 4: Add GitHub Secret

1. Navigate to your GitHub repository
2. Go to **Settings** > **Secrets and variables** > **Actions**
3. Click **New repository secret**
4. Enter the details:
   - **Name:** `NUGET_API_KEY`
   - **Secret:** Paste the NuGet API key
5. Click **Add secret**

## 3. Verify Setup

### Verify Secrets Are Added

1. Go to **Settings** > **Secrets and variables** > **Actions**
2. You should see two repository secrets:
   - `CODECOV_TOKEN`
   - `NUGET_API_KEY`

### Test the CI/CD Pipeline

1. Make a small change to a file (e.g., add a comment)
2. Commit and push to a branch:
   ```bash
   git checkout -b test-cicd
   git add .
   git commit -m "Test CI/CD pipeline"
   git push origin test-cicd
   ```
3. Create a pull request
4. Check the **Actions** tab to see the pipeline running
5. Verify:
   - All jobs complete successfully
   - Coverage report is uploaded to Codecov
   - Badges in README are working

### Test Coverage Upload

1. After the CI/CD pipeline completes, go to https://codecov.io/gh/jas88/CsvHandler
2. You should see the coverage report for your branch
3. Check that coverage percentages are displayed

## 4. GitHub Environment Setup (Optional but Recommended)

For additional protection, configure a production environment:

### Create Production Environment

1. Go to **Settings** > **Environments**
2. Click **New environment**
3. Enter name: `production`
4. Configure protection rules:
   - **Required reviewers:** Add yourself or team members
   - **Wait timer:** Set to 0 (or a delay if desired)
   - **Deployment branches:** Select "Selected branches" and add `main`
5. Click **Save protection rules**

### Why Use Environments?

- Prevents accidental package publishing
- Requires manual approval for releases
- Provides audit trail for deployments
- Can restrict deployments to specific branches

## 5. Testing Package Publishing

### Create a Test Release

1. Update version in `Directory.Build.props` (e.g., `0.1.0-test`)
2. Commit and push to main:
   ```bash
   git add Directory.Build.props
   git commit -m "Bump version to 0.1.0-test"
   git push origin main
   ```
3. Create a GitHub release:
   - Go to **Releases** > **Draft a new release**
   - Click **Choose a tag** > Enter `v0.1.0-test` > Create new tag
   - Release title: `v0.1.0-test - Test Release`
   - Description: `Test release to verify CI/CD pipeline`
   - Check **This is a pre-release**
   - Click **Publish release**
4. Check **Actions** tab to see the publish job running
5. If using production environment, approve the deployment
6. Verify package appears on NuGet.org

### Verify Published Package

1. Navigate to https://www.nuget.org/packages/CsvHandler
2. Verify your test version is listed
3. Click on the version to see package details

## 6. Troubleshooting

### Codecov Upload Fails

**Symptom:** Codecov upload step fails with "Unable to upload coverage"

**Solutions:**
1. Verify CODECOV_TOKEN is set correctly
2. Check that coverage files are being generated:
   ```yaml
   - name: List coverage files
     run: find ./coverage -name "*.xml"
   ```
3. Ensure you have public repository access or valid token
4. Try regenerating the Codecov token

### NuGet Push Fails

**Symptom:** Package push fails with 401 Unauthorized or 403 Forbidden

**Solutions:**
1. Verify NUGET_API_KEY is set correctly
2. Check API key hasn't expired
3. Verify glob pattern matches your package ID
4. Ensure API key has push permissions
5. Check if package version already exists (use `--skip-duplicate`)

### API Key Expired

**Symptom:** NuGet push suddenly fails after working previously

**Solution:**
1. Generate a new API key in NuGet.org
2. Update the GitHub secret with the new key
3. Re-run the failed workflow

### Coverage Report Not Appearing

**Symptom:** Build succeeds but no coverage on Codecov

**Solutions:**
1. Check that tests are running: `dotnet test --verbosity normal`
2. Verify coverage collector is installed: Check test project for `coverlet.collector`
3. Look at coverage file location: Check upload step for correct file paths
4. Ensure `codecov.yml` doesn't exclude all files

## 7. Best Practices

### API Key Security

- **Never commit API keys** to source control
- **Rotate API keys** every 6-12 months
- **Use minimal permissions** - only grant necessary scopes
- **Monitor usage** - check NuGet.org for API key activity
- **Revoke immediately** if compromised

### Secret Management

- **Document secret names** in this guide
- **Update team members** when rotating secrets
- **Use descriptive names** for API keys (e.g., "GitHub Actions - CsvHandler")
- **Set calendar reminders** for key rotation
- **Audit access** regularly

### Release Process

- **Test in pre-release** first (e.g., `-alpha`, `-beta`)
- **Review code coverage** before major releases
- **Update documentation** before publishing
- **Create detailed release notes** for users
- **Monitor for issues** after publishing

## 8. Next Steps

After completing the setup:

1. **Verify badges** in README.md are displaying correctly
2. **Review codecov.yml** configuration and adjust thresholds if needed
3. **Test full release process** with a pre-release version
4. **Document release process** for team members
5. **Set up branch protection** rules for main branch
6. **Configure required status checks** (CI must pass before merging)

## Support Resources

- **GitHub Actions:** https://docs.github.com/en/actions
- **Codecov Documentation:** https://docs.codecov.com
- **NuGet Documentation:** https://docs.microsoft.com/en-us/nuget/
- **Repository Issues:** https://github.com/jas88/CsvHandler/issues

For additional help, create an issue in the repository or consult the documentation links above.
