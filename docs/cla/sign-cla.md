# Signing the contributor licence agreement

ERP-AI-Pro accepts a contribution only from people covered by a contributor licence agreement with Dewiride Technologies Private Limited, the copyright holder:

- [ERP-AI-Pro Individual Contributor Licence Agreement v1.0](individual-cla-1.0.md), when you contribute for yourself;
- [ERP-AI-Pro Corporate Contributor Licence Agreement v1.0](corporate-cla-1.0.md), when a company owns what you write (your employer or a client) and signs for you.

You keep the copyright in your contribution. The agreement lets Dewiride Technologies Private Limited license it under other terms as well, including in the proprietary Enterprise edition, on the condition that it is always also licensed under the GNU Lesser General Public License version 3 only (`LGPL-3.0-only`), the licence of this repository. Read the agreement before you sign it.

## Who signs

- Every person whose commits appear in a pull request, and every person named in a `Co-authored-by:` trailer of one of those commits, is covered by a signature before the pull request can merge. Coverage is decided by email address: the author email of each commit and the email of each `Co-authored-by:` trailer, compared without regard to letter case.
- Unless exempt (see [Exemptions](#exemptions)), the person who opens a pull request is named by a signature too, by GitHub login: their own individual signature, or a line with their login under a company's `List of contributors:`.
- The individual agreement is signed only by a person at least eighteen years old.
- If you are employed, or write the contribution for a client, and that company owns what you write, the company signs the corporate agreement and lists you as a contributor, or approves your individual signature (section 3(c) of the individual agreement).
- A bot account never signs.

## Exemptions

[`exempt.json`](exempt.json) lists the pull request authors who do not sign: the copyright holder's own account and automation accounts. Each entry names the login, the reason and the emails that login commits with.

An exempt email covers the author of a commit only in a pull request opened by an exempt login: the copyright holder's own pull requests, and the Dependabot pull requests the copyright holder adds commits to. A commit authored with an exempt address in anyone else's pull request is not covered, so nobody can skip signing by committing with the copyright holder's address. An exempt email in a `Co-authored-by:` trailer is covered in every pull request, so a contributor who accepts a suggestion from the copyright holder's review stays covered. A signature never lists an exempt email.

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
- List every email address you commit with, including the GitHub no-reply address (`<id>+<login>@users.noreply.github.com`) that commits made on github.com use. A no-reply address is always the one of the login on its line. The commands at the end of [The check](#the-check) show the addresses on your branch.
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
- Exactly one signatory line, `<name> <email> https://github.com/<login>`, for the person who signs for the company. The signatory's email address belongs to this file like every other address in it: it is never an exempt email and no other signature file lists it.
- `List of contributors:`, then at least one line, in the same shape, for every email address of every person who contributes on the company's behalf. The signatory is covered, and may open pull requests, only when listed as a contributor as well.
- A no-reply address is always the one of the login on its line, as for an individual.
- The pull request that adds the file is opened by the signatory. The signatory adds contributors by a later pull request that edits the file; only the signatory the base branch's version of the file names may edit it, so that signatory is also the one who hands the signature over to a new signatory.

## Keep your signature current

- Add a line to your signature file in a later pull request when you start committing with another email address; for a company, the signatory adds it.
- Each email address appears in exactly one signature file.
- A signature file is never deleted or renamed: the agreement is irrevocable for the contributions already made under it.

## Work you do not own

The agreements refer to this section for work whose copyright you, or the company that signed for you, do not own in full.

- Work that belongs to your employer or a client is contributed only under that company's corporate signature, or with its approval of your individual signature.
- Any other part of a contribution that someone else owns (code, text, images or data from another project or author) is identified in the pull request description before the pull request merges: the files it is in, where it comes from, who owns its copyright and the licence it is under. Its copyright and licence notices stay intact, the description marks it "Not a Contribution", and it is included only when its licence lets it be distributed as part of ERP-AI-Pro under `LGPL-3.0-only`.

## The check

The workflow `cla` (job "Contributor licence agreement") runs on every pull request through the `pull_request_target` event, so nothing a pull request changes can alter the check that judges it: the workflow file comes from the default branch, and `scripts/checks/cla.ts` and `exempt.json` come from the pull request's base commit. The pull request's commits and signature files are only read, never checked out or run. For the same reason the workflow does not run on the pull request that introduces it, and a pull request that changes it is checked by the version already on the default branch; a new version runs from the default branch once its pull request has merged.

The commits checked are the ones on the pull request's head that are not on the base branch as it is when the check runs. The check fails when:

- a commit's author email is not covered: an exempt email is covered as an author only in a pull request opened by an exempt login, and any other email only when a well-formed signature lists it;
- a `Co-authored-by:` email is not covered: an exempt email is covered as a co-author in every pull request, and any other email only when a well-formed signature lists it; a trailer that, with its continuation lines joined, does not hold exactly one `<name> <email>` is reported whole as uncovered;
- the pull request was opened by a login that is not exempt and that no well-formed signature names, either as the file name of an individual signature or as a login under a company's `List of contributors:` (a company's signatory alone does not count);
- a signature file is malformed, named outside the rules above, not a regular file, lists a GitHub no-reply address of a login other than the one on its line, or lists an email address (including a company signatory's) that another signature or an exemption already holds;
- an individual signature other than the one named after the pull request's author is added or edited;
- a corporate signature is added by anyone but the signatory it names, or edited by anyone but the signatory the base branch's version of it names;
- a signature file is deleted or renamed.

A pull request opened by an exempt login may add or edit any signature, but never delete or rename one.

A failure lists each uncovered commit as `<short sha> <email> (author|co-author)`. Add or correct your signature in the same pull request and push again.

Run the same checks before you push, against this repository's `main` branch rather than your fork's:

- `node scripts/checks/cla.ts` checks every signature file in your working tree;
- `git fetch https://github.com/Dewiride-Open-Source/ERP-AI-Pro.git main` fetches the upstream branch into `FETCH_HEAD`;
- `git log --format='%ae%n%(trailers:key=Co-authored-by,valueonly)' FETCH_HEAD..HEAD` then shows the author and `Co-authored-by:` addresses on your branch, each of which needs coverage: yours by your signature, a co-author's by their own;
- `node scripts/checks/cla.ts --base FETCH_HEAD --head HEAD --pull-request-author <your-github-login>` then also checks the commits on your branch.
