# Signing the contributor licence agreement

ERP-AI-Pro accepts a contribution only from people covered by a contributor licence agreement with Dewiride Technologies Private Limited, the copyright holder:

- [ERP-AI-Pro Individual Contributor Licence Agreement v1.0](individual-cla-1.0.md), when you contribute for yourself;
- [ERP-AI-Pro Corporate Contributor Licence Agreement v1.0](corporate-cla-1.0.md), when a company owns what you write (your employer or a client) and signs for you.

You keep the copyright in your contribution. The agreement lets Dewiride Technologies Private Limited license it under other terms as well, including in the proprietary Enterprise edition, on the condition that it is always also licensed under the GNU Lesser General Public License version 3 only (`LGPL-3.0-only`), the licence of this repository. Read the agreement before you sign it.

## Who signs

- Every person whose commits appear in a pull request, and every person named in a `Co-authored-by:` trailer of one of those commits, is covered by a signature before the pull request can merge. Coverage is decided by email address: the author email of each commit and the email of each `Co-authored-by:` trailer, compared without regard to letter case.
- If you are employed, or write the contribution for a client, and that company owns what you write, the company signs the corporate agreement and lists you as a contributor, or approves your individual signature (section 3(c) of the individual agreement).
- A bot account never signs.

## Exemptions

[`exempt.json`](exempt.json) lists the pull request authors who do not sign: the copyright holder's own account and automation accounts. Each entry names the login, the reason and the emails that login commits with.

An exempt email counts only in a pull request opened by an exempt login: the copyright holder's own pull requests, and the Dependabot pull requests the copyright holder adds commits to. A commit made with an exempt address in anyone else's pull request is not covered, so nobody can skip signing by committing with the copyright holder's address. A signature never lists an exempt email, and a co-author is exempt only through an exempt email in such a pull request.

## Sign as an individual

In your first pull request, add `docs/cla/individual/<lowercase-github-login>.md`: for the GitHub account `JaneDoe`, the file is `docs/cla/individual/janedoe.md`.

```text
India, 2026-10-01

I hereby agree to the terms of the ERP-AI-Pro Individual Contributor Licence Agreement v1.0.

I declare that I am authorised and able to make this agreement and sign this declaration.

Signed,

Jane Doe jane@example.com https://github.com/janedoe
Jane Doe 12345+janedoe@users.noreply.github.com https://github.com/janedoe
```

- The first line is your country and the date you sign, `<country>, <YYYY-MM-DD>`; the date is never later than today.
- The two statements and `Signed,` are copied exactly.
- Then one line per email address, `<name> <email> https://github.com/<login>`, every line with your own login, the one in the file name.
- List every email address you commit with, including the GitHub no-reply address (`<id>+<login>@users.noreply.github.com`) that commits made on github.com use. `git log --format=%ae origin/main..HEAD` shows the addresses on your branch.
- Blank lines are ignored; every other line is part of the signature.

## Sign for a company

An individual authorised to sign for the company adds `docs/cla/corporate/<company-slug>.md`, where the slug is the company name in lowercase words joined by hyphens: `docs/cla/corporate/example-private-limited.md` for Example Private Limited.

```text
India, 2026-10-01

Example Private Limited agrees to the terms of the ERP-AI-Pro Corporate Contributor Licence Agreement v1.0.

I declare that I am authorised and able to make this agreement and sign this declaration on behalf of the company named above.

Signed,

Asha Rao asha.rao@example.com https://github.com/asharao

List of contributors:

Asha Rao asha.rao@example.com https://github.com/asharao
Ravi Kumar ravi.kumar@example.com https://github.com/ravikumar
```

- The first line is the country and the date of signature, as for an individual.
- The statement starts with the company's legal name; the declaration and `Signed,` are copied exactly.
- Exactly one signatory line, `<name> <email> https://github.com/<login>`, for the person who signs for the company.
- `List of contributors:`, then at least one line, in the same shape, for every email address of every person who contributes on the company's behalf. The signatory is covered only when listed as a contributor as well.
- The company adds contributors by a later pull request that edits the file.

## Keep your signature current

- Add a line to your signature file in a later pull request when you start committing with another email address.
- Each email address appears in exactly one signature file.
- A signature file is never deleted or renamed: the agreement is irrevocable for the contributions already made under it.

## Work you do not own

The agreements refer to this section for work whose copyright you, or the company that signed for you, do not own in full.

- Work that belongs to your employer or a client is contributed only under that company's corporate signature, or with its approval of your individual signature.
- Any other part of a contribution that someone else owns (code, text, images or data from another project or author) is identified in the pull request description before the pull request merges: the files it is in, where it comes from, who owns its copyright and the licence it is under. Its copyright and licence notices stay intact, the description marks it "Not a Contribution", and it is included only when its licence lets it be distributed as part of ERP-AI-Pro under `LGPL-3.0-only`.

## The check

The workflow `cla` (job "Contributor licence agreement") runs on every pull request. It runs `scripts/checks/cla.ts` from the pull request's base commit, reads the exemptions from the base commit and the signatures from the pull request's head, and fails when:

- a commit author or `Co-authored-by:` email in the pull request is neither an exempt email in a pull request opened by an exempt login nor listed in a well-formed signature;
- a signature file is malformed, named outside the rule above, or lists an email address that another signature or an exemption already holds;
- an individual signature other than the one named after the pull request's author is added or edited (an exempt author may add or edit any);
- a signature file is deleted or renamed.

A failure lists each uncovered commit as `<short sha> <email> (author|co-author)`. Add or correct your signature in the same pull request and push again.

Run the same checks before you push:

- `node scripts/checks/cla.ts` checks every signature file in your working tree;
- `node scripts/checks/cla.ts --base origin/main --head HEAD --pull-request-author <your-github-login>` also checks the commits on your branch.
