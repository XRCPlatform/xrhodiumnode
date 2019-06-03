# Contributing to Bitcoin Rhodium

The Bitcoin Rhodium project welcomes contributions, code reviews, testing, patches, bug fixes, etc. The document outlines guidelines and processes that are involved with contributing to the project.

For practical and security reasons, there are a few primary maintainers of the official repository who will handle any releases but also maintain the integrity of the project officially. They are referred to as the development team, but they are not privileged other than keeping the project in check and moving ahead and maintaining a roadmap.

# How can I start contributing?

Large projects like these need people with many skills, whether those who can code, those who can write documentation, etc. The first place to start is our Discord room at [https://discord.gg/WmxceSm]. Following our open development process happens in the #development room.

Bitcoin Rhodium's Node utilizes .NET Core and is primarily written in C#. There might be other applications that are written in other languages. If you are new to C# development a good place to start is [https://dotnet.microsoft.com/].

# Recording Issues

We currently have an issue tracker at [https://issues.bitcoinrh.org]. Please refer to this site for understanding the current issues and for logging new ones.

# Workflow

Everyone can make contributions, but it is best when a certain workflow is utilized.

All activity happens through out GitLab account: [https://gitlab.com/bitcoinrh].

An ideal contribution has a single purpose, that sufficiently states the reason for the change and references issues that it is trying to address.

To start, fork the repository, create a topic/feature branch and start committing to it.

## Commits

Commits should be as small as possible doing only one clear thing. It is easier to scan and understand a change when the commits are clear enough and don't look too random.

Commit messages should be explanatory as to what exactly is changing. It is suggested to keep subject lines short and the body brief enough to fully explain what was changed. If the subject describes something succinctly, then there is no need for further explanation.

_Please do not include reproducible binaries in commits._ They add bloat to the repository for little reason. Always prefer code and scripts to generate these type of files. You will be asked to redo the commit if this happens.

## Merge Requests

A merge request is a request to merge into the main repository from your forked version. GitLab provides an interface to use this as an opportunity for code review.

_Merge requests are excellent ways to document change, outside of small commits._ It gives context to what is being changed and why. This documentation, from both commit and merge request, comes in use when we ask "Why did this change?"

A merge request subject and description should state the component. It would help if you start the subject with clearly identifiable tag (like in square brackets).

 * [Consensus] : changing critical consensus code,
 * [Documentation] : changes to documentation
 * [Wallet] : changes to wallets
 * [P2P] : For low-level server code
 * [RPC] : For RPC fixes
 * [Test] : Adding, removing and fixing tests
 * [Codebase] : For code cleanup, etc.
 * [Utility] : For anything that would be considered utilities

Examples:

1. [Consensus] Make changes necessary to activate SegWit
2. [RPC] Add deprecated wallet RPCs necessary for certain mining pools.

## Code Reviews

Code reviews are important for non-trivial systems, and Bitcoin Rhodium is no exception. In our case, the product itself has value and depends on the correct operation of this software to help towards maintaining its value. As such, we take contributions seriously in this light. Not everything requires deep review though, but ensuring at least more than one person knows what is going into the codebase is really important.

We consider all requests important if there is a good reason for us to pull it in. the questions we try ask are:

 1. Does it have a solid "business" or "technical" purpose?
 2. Is the implementation sufficient? Does it fit in well given the knowledge everyone has of the current system?
 3. Is everything properly documented and can be useful to reference in the future?

 If these criteria are met, it will be merged. This only applies to the main repository. You're using Git, do whatever you want otherwise!

Consensus-level code gets a lot more scrutiny, even if it is a refactor. Lean on tests for these and write more if necessary.

 ### Who should be selected to approve it?

 There is an option to select someone to review. It is not necessary to assign anyone. When it is in the merge request list, it will be picked up and discussed.

### Peers Indicating MR Viability

Bitcoin Core uses a shorthand way to describe the level of acceptance and we will find it useful to adopt as well. The language suggests to the maintainer if it is ready to go in or not.

 * (t)ACK: "I tested the code and I agree it should be merged."
 * NACK: "I disagree this should be merged". This can apply to both technical and legal reasons.
 * utACK: "I have not tested the code, but I have reviewed it, I agree it can be merged"
 * Concept ACK: "I agree with the general principle of the request"
 * Nit: "Just do it, this is a trivial change".

# Code Style

Even if the code base is not perfect, we want to encourage a good codebase anyway.

We are adopting (.NET Core's guidelines)[https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md].

With these in mind, there is one major thing that would help a lot. Please don't add extraneous whitespace. Use your IDE/editor's "Remove extraneous whitespace on save" feature. If there is extraneous whitespace in existing code, please just make a separate [Codebase] merge request to clean them up. Extraneous whitespace is noise and hard to read changes with that noise.

 # Copyright

 By contributing to the primary repository, you agree to license your code with the MIT license. If you are contributing code that you didn't write, it must follow with whatever is required for the license. The code should be compatible with the MIT license and should not cause issue for formal distribution.